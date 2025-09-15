using System.Collections.Concurrent;
using System.Threading.Channels;
using GrpcService.Models;

namespace GrpcService.Infrastructure
{
    public class GrpcRequestQueueService : BackgroundService
    {
        private readonly Channel<TaskRequestItem> _queue;
        private readonly ILogger<GrpcRequestQueueService> _logger;
        private readonly DeviceLoggerService _deviceLogger;
        private readonly TenantConcurrencyManager _tenantConcurrency;
        private readonly int _maxConcurrency;
        private readonly TimeSpan _requestTimeout;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly ConcurrentDictionary<string, Task> _processingTasks = new();
        private long _processedRequests = 0;

        public GrpcRequestQueueService(
            ILogger<GrpcRequestQueueService> logger,
            IConfiguration configuration,
            DeviceLoggerService deviceLogger,
            TenantConcurrencyManager tenantConcurrency)
        {
            _logger = logger;
            _deviceLogger = deviceLogger;
            _tenantConcurrency = tenantConcurrency;

            var capacity = configuration.GetValue("GrpcService:RequestQueueCapacity", 10000);
            _queue = Channel.CreateBounded<TaskRequestItem>(capacity);

            _maxConcurrency = configuration.GetValue("GrpcService:MaxConcurrentRequests", Environment.ProcessorCount * 2);
            _concurrencySemaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            var timeoutSeconds = configuration.GetValue("GrpcService:RequestTimeoutSeconds", 30);
            _requestTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        public async Task<TResponse> EnqueueAsync<TRequest, TResponse>(
         string deviceId,
         Func<TRequest, CancellationToken, Task<TResponse>> handler,
         TRequest request,
         CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>();
            var item = new TaskRequestItem
            {
                DeviceId = deviceId,
                Handler = async (req, ct) =>
                {
                    var result = await handler((TRequest)req, ct);
                    return result!; 
                },
                Request = request!,
                RequestId = Guid.NewGuid().ToString(),
                TaskCompletionSource = tcs,
                CancellationToken = cancellationToken
            };

            if (!_queue.Writer.TryWrite(item))
            {
                throw new InvalidOperationException("Request queue is full.");
            }

            var result = await tcs.Task.WaitAsync(_requestTimeout, cancellationToken);
            return (TResponse)result; // 转换回原始类型
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GrpcRequestQueueService started.");

            await foreach (var TaskRequestItem in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await _concurrencySemaphore.WaitAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var startTime = DateTime.Now;

                        using (await _tenantConcurrency.AcquireAsync(TaskRequestItem.DeviceId, TaskRequestItem.CancellationToken))
                        {
                            var result = await TaskRequestItem.Handler(TaskRequestItem.Request, TaskRequestItem.CancellationToken);

                            var duration = DateTime.Now - startTime;
                            _deviceLogger.LogDeviceInfo(TaskRequestItem.DeviceId,
                                "请求处理完成: {RequestId}, Duration: {Duration}ms",
                                TaskRequestItem.RequestId, duration.TotalMilliseconds);

                            TaskRequestItem.TaskCompletionSource.SetResult(result);
                            Interlocked.Increment(ref _processedRequests);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "请求处理异常: {RequestId}", TaskRequestItem.RequestId);
                        TaskRequestItem.TaskCompletionSource.SetException(ex);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, stoppingToken);
            }
        }
    }
}
