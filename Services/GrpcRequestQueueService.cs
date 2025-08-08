using GrpcService.HKSDK;
using GrpcService.Models;
using System.Threading.Channels;

namespace GrpcService.Services
{
    public class GrpcRequestQueueService : BackgroundService, IGrpcRequestQueueService
    {
        private readonly ILogger<GrpcRequestQueueService> _logger;
        private readonly CMSService _cmsService;
        private readonly IDeviceLoggerService _deviceLogger;
        private readonly Channel<object> _requestChannel;
        private readonly ChannelWriter<object> _requestWriter;
        private readonly ChannelReader<object> _requestReader;
        private readonly SemaphoreSlim _processingLimit;
        private readonly int _maxConcurrentRequests;
        private readonly int _requestTimeoutMinutes;
        private long _processedRequests = 0;
        private long _failedRequests = 0;
        private long _timeoutRequests = 0;
        private readonly object _disposeLock = new();
        private bool _disposed = false;


        public GrpcRequestQueueService(
            ILogger<GrpcRequestQueueService> logger,
            IDeviceLoggerService deviceLogger,
            IConfiguration configuration)
        {
            _logger = logger;
            _deviceLogger = deviceLogger;

            var config = configuration.GetSection("HikDevice");
            _maxConcurrentRequests = config.GetValue<int>("MaxConcurrentRequests", 20);
            _requestTimeoutMinutes = config.GetValue<int>("CommandTimeoutMinutes", 2);
            var queueCapacity = config.GetValue<int>("QueueCapacity", 1000);

            // 创建有界队列防止内存溢出
            var options = new BoundedChannelOptions(queueCapacity)
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            };

            _requestChannel = Channel.CreateBounded<object>(options);
            _requestWriter = _requestChannel.Writer;
            _requestReader = _requestChannel.Reader;

            _processingLimit = new SemaphoreSlim(_maxConcurrentRequests, _maxConcurrentRequests);

            _logger.LogInformation("gRPC请求队列服务初始化完成，最大并发请求: {MaxConcurrent}, 队列容量: {QueueCapacity}",
                _maxConcurrentRequests, queueCapacity);
        }

        public async Task<TResponse> EnqueueRequestAsync<TRequest, TResponse>(
            string deviceId,
            string requestType,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TResponse>> handler,
            CancellationToken cancellationToken = default)
        {
            var requestItem = new GrpcRequestItem<TRequest, TResponse>
            {
                DeviceId = deviceId,
                Request = request,
                RequestType = requestType,
                Handler = handler,
                CancellationToken = cancellationToken
            };

            try
            {
                // 入队请求
                await _requestWriter.WriteAsync(requestItem, cancellationToken);

                _logger.LogDebug("请求已入队: {RequestId}, DeviceId: {DeviceId}, Type: {RequestType}",
                    requestItem.RequestId, deviceId, requestType);

                // 等待处理完成
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(_requestTimeoutMinutes));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    return await requestItem.TaskCompletionSource.Task;
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    Interlocked.Increment(ref _timeoutRequests);
                    _deviceLogger.LogDeviceError(deviceId, null, "请求处理超时: {RequestType}", requestType);
                    throw new TimeoutException($"请求处理超时: {requestType}");
                }
            }
            catch (InvalidOperationException)
            {
                _logger.LogError("队列已关闭，无法处理新请求");
                throw new InvalidOperationException("服务正在关闭，无法处理新请求");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("gRPC请求队列处理服务启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var item in _requestReader.ReadAllAsync(stoppingToken))
                    {
                        await _processingLimit.WaitAsync(stoppingToken);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessRequestAsync(item);
                            }
                            finally
                            {
                                _processingLimit.Release();
                            }
                        }, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("gRPC请求队列处理服务正常停止");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "gRPC请求队列处理服务异常，将在3秒后自动重试");
                    await Task.Delay(3000, stoppingToken);
                }
            }
        }

        private async Task ProcessRequestAsync(object requestItem)
        {
            // 使用反射处理泛型请求
            var requestType = requestItem.GetType();
            if (!requestType.IsGenericType) return;

            try
            {
                var method = GetType().GetMethod(nameof(ProcessTypedRequestAsync),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var genericMethod = method?.MakeGenericMethod(requestType.GetGenericArguments());

                if (genericMethod != null)
                {
                    await (Task)genericMethod.Invoke(this, new[] { requestItem })!;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理请求异常: {RequestType}", requestType.Name);
            }
        }

        private async Task ProcessTypedRequestAsync<TRequest, TResponse>(
            GrpcRequestItem<TRequest, TResponse> requestItem)
        {
            try
            {
                var startTime = DateTime.Now;
                _deviceLogger.LogDeviceDebug(requestItem.DeviceId,
                    "开始处理请求: {RequestId}, Type: {RequestType}",
                    requestItem.RequestId, requestItem.RequestType);

                var result = await requestItem.Handler(requestItem.Request, requestItem.CancellationToken);

                var duration = DateTime.Now - startTime;
                _deviceLogger.LogDeviceInfo(requestItem.DeviceId,
                    "请求处理完成: {RequestId}, Duration: {Duration}ms",
                    requestItem.RequestId, duration.TotalMilliseconds);

                requestItem.TaskCompletionSource.SetResult(result);
                Interlocked.Increment(ref _processedRequests);
            }
            catch (OperationCanceledException)
            {
                _deviceLogger.LogDeviceWarning(requestItem.DeviceId, "请求被取消: {RequestId}", requestItem.RequestId);
                requestItem.TaskCompletionSource.SetCanceled();
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(requestItem.DeviceId, ex, "请求处理异常: {RequestId}", requestItem.RequestId);
                requestItem.TaskCompletionSource.SetException(ex);
                Interlocked.Increment(ref _failedRequests);
            }
        }

        public Dictionary<string, object> GetQueueStatistics()
        {
            return new Dictionary<string, object>
            {
                ["max_concurrent_requests"] = _maxConcurrentRequests,
                ["available_slots"] = _processingLimit.CurrentCount,
                ["processed_requests"] = _processedRequests,
                ["failed_requests"] = _failedRequests,
                ["timeout_requests"] = _timeoutRequests,
                ["last_update"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public override void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                try
                {
                    _logger.LogInformation("开始清理gRPC请求队列服务...");

                    // 停止接收新请求
                    _requestWriter?.Complete();

                    // 释放信号量
                    _processingLimit?.Dispose();

                    _logger.LogInformation("gRPC请求队列服务清理完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理gRPC请求队列服务时发生异常");
                }
                finally
                {
                    _disposed = true;
                    base.Dispose();
                }
            }
        }
    }
}
