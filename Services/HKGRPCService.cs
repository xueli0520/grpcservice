using Grpc.Core;
using GrpcService;
using GrpcService.HKSDK.service;
using GrpcService.Models;
using System.Threading.Channels;

namespace GrpcService.Services
{
    public class HikDeviceService : HikDevicegRPCService.HikDevicegRPCServiceBase
    {
        private readonly ILogger<HikDeviceService> _logger;
        private readonly OptimizedDeviceManager _deviceManager;
        private readonly CMSService _cmsService;
        private readonly Channel<GrpcRequestMessage> _requestQueue;
        private readonly SemaphoreSlim _concurrencyLimiter;

        public HikDeviceService(
            ILogger<HikDeviceService> logger,
            OptimizedDeviceManager deviceManager,
            CMSService cmsService)
        {
            _logger = logger;
            _deviceManager = deviceManager;
            _cmsService = cmsService;

            // 创建gRPC请求处理队列
            var queueOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _requestQueue = Channel.CreateBounded<GrpcRequestMessage>(queueOptions);

            // 限制并发处理数量
            _concurrencyLimiter = new SemaphoreSlim(50); // 最多50个并发请求

            // 启动请求处理器
            _ = Task.Run(ProcessGrpcRequests);
        }

        #region 队列处理

        private async Task ProcessGrpcRequests()
        {
            _logger.LogInformation("gRPC请求处理器启动");

            await foreach (var request in _requestQueue.Reader.ReadAllAsync())
            {
                _ = Task.Run(async () =>
                {
                    await _concurrencyLimiter.WaitAsync();
                    try
                    {
                        await ProcessGrpcRequest(request);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                });
            }
        }

        private async Task ProcessGrpcRequest(GrpcRequestMessage request)
        {
            try
            {
                _logger.LogDebug($"处理gRPC请求: {request.RequestType}, DeviceId: {request.DeviceId}");

                switch (request.RequestType)
                {
                    case "OpenDoor":
                        await ProcessOpenDoorRequest(request);
                        break;
                    case "Reboot":
                        await ProcessRebootRequest(request);
                        break;
                    case "GetDeviceInfo":
                        await ProcessGetDeviceInfoRequest(request);
                        break;
                    // 添加其他请求类型
                    default:
                        request.CompletionSource.SetException(
                            new InvalidOperationException($"不支持的请求类型: {request.RequestType}"));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理gRPC请求异常: {request.RequestType}");
                request.CompletionSource.SetException(ex);
            }
        }

        private async Task<T> EnqueueRequest<T>(string requestType, string deviceId, object requestData)
        {
            var request = new GrpcRequestMessage
            {
                RequestType = requestType,
                DeviceId = deviceId,
                RequestData = requestData,
                CompletionSource = new TaskCompletionSource<object>()
            };

            if (!await _requestQueue.Writer.WaitToWriteAsync())
            {
                throw new InvalidOperationException("gRPC请求队列已关闭");
            }

            await _requestQueue.Writer.WriteAsync(request);
            var result = await request.CompletionSource.Task;
            return (T)result;
        }

        #endregion

        #region gRPC接口实现

        public override async Task<OpenDoorResponse> OpenDoor(
            OpenDoorRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"接收开门请求: DeviceId={request.DeviceId}, Operator={request.Operator}");

                // 验证请求参数
                if (string.IsNullOrEmpty(request.DeviceId))
                {
                    return new OpenDoorResponse
                    {
                        Success = false,
                        Message = "设备ID不能为空",
                        ErrorCode = "INVALID_DEVICE_ID",
                        DeviceId = request.DeviceId
                    };
                }

                // 通过队列处理请求
                var response = await EnqueueRequest<OpenDoorResponse>("OpenDoor", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"开门操作异常: {request.DeviceId}");
                return new OpenDoorResponse
                {
                    Success = false,
                    Message = $"开门操作异常: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        private async Task ProcessOpenDoorRequest(GrpcRequestMessage grpcRequest)
        {
            var request = (OpenDoorRequest)grpcRequest.RequestData;

            try
            {
                // 执行开门命令
                var commandResult = await _deviceManager.ExecuteDeviceCommandAsync(
                    request.DeviceId,
                    "OpenDoor",
                    new Dictionary<string, object>
                    {
                        ["operator"] = request.Operator,
                        ["messageId"] = request.MessageId
                    });

                var response = new OpenDoorResponse
                {
                    Success = commandResult.Success,
                    Message = commandResult.Message,
                    ErrorCode = commandResult.ErrorCode,
                    DeviceId = request.DeviceId
                };

                grpcRequest.CompletionSource.SetResult(response);
            }
            catch (Exception ex)
            {
                grpcRequest.CompletionSource.SetException(ex);
            }
        }

        public override async Task<RebootResponse> Reboot(
            RebootRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"接收重启请求: DeviceId={request.DeviceId}, Operator={request.Operator}");

                var response = await EnqueueRequest<RebootResponse>("Reboot", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"重启操作异常: {request.DeviceId}");
                return new RebootResponse
                {
                    Success = false,
                    Message = $"重启操作异常: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        private async Task ProcessRebootRequest(GrpcRequestMessage grpcRequest)
        {
            var request = (RebootRequest)grpcRequest.RequestData;

            try
            {
                var commandResult = await _deviceManager.ExecuteDeviceCommandAsync(
                    request.DeviceId,
                    "Reboot",
                    new Dictionary<string, object>
                    {
                        ["operator"] = request.Operator,
                        ["messageId"] = request.MessageId
                    });

                var response = new RebootResponse
                {
                    Success = commandResult.Success,
                    Message = commandResult.Message,
                    ErrorCode = commandResult.ErrorCode,
                    DeviceId = request.DeviceId
                };

                grpcRequest.CompletionSource.SetResult(response);
            }
            catch (Exception ex)
            {
                grpcRequest.CompletionSource.SetException(ex);
            }
        }

        public override async Task<GetDeviceInfoResponse> GetDeviceInfo(
            GetDeviceInfoRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"接收获取设备信息请求: DeviceId={request.DeviceId}");

                var response = await EnqueueRequest<GetDeviceInfoResponse>("GetDeviceInfo", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取设备信息异常: {request.DeviceId}");
                return new GetDeviceInfoResponse
                {
                    Success = false,
                    Message = $"获取设备信息异常: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        private async Task ProcessGetDeviceInfoRequest(GrpcRequestMessage grpcRequest)
        {
            var request = (GetDeviceInfoRequest)grpcRequest.RequestData;

            try
            {
                var commandResult = await _deviceManager.ExecuteDeviceCommandAsync(
                    request.DeviceId,
                    "GetDeviceInfo",
                    new Dictionary<string, object>
                    {
                        ["operator"] = request.Operator,
                        ["messageId"] = request.MessageId
                    });

                var response = new GetDeviceInfoResponse
                {
                    Success = commandResult.Success,
                    Message = commandResult.Message,
                    ErrorCode = commandResult.ErrorCode,
                    DeviceId = request.DeviceId
                };

                // 如果命令执行成功，填充设备信息
                if (commandResult.Success && commandResult.Data.Count > 0)
                {
                    response.DeviceInfo = new DeviceInfo
                    {
                        DeviceId = commandResult.Data.GetValueOrDefault("device_id")?.ToString() ?? request.DeviceId,
                        DeviceName = commandResult.Data.GetValueOrDefault("device_name")?.ToString() ?? "",
                        FirmwareVersion = commandResult.Data.GetValueOrDefault("firmware_version")?.ToString() ?? "",
                        IpAddress = commandResult.Data.GetValueOrDefault("ip_address")?.ToString() ?? "",
                        UserCount = Convert.ToInt32(commandResult.Data.GetValueOrDefault("user_count", 0)),
                        LastOnlineTime = Convert.ToInt64(commandResult.Data.GetValueOrDefault("last_online_time", 0))
                    };
                }

                grpcRequest.CompletionSource.SetResult(response);
            }
            catch (Exception ex)
            {
                grpcRequest.CompletionSource.SetException(ex);
            }
        }

        public override async Task<GetDeviceStatusResponse> GetDeviceStatus(
            GetDeviceStatusRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"接收获取设备状态请求: DeviceId={request.DeviceId}");

                // 直接从设备管理器获取状态，不需要队列处理
                var onlineDevices = _deviceManager.GetOnlineDevices();
                var device = onlineDevices.FirstOrDefault(d => d.DeviceId == request.DeviceId);

                if (device == null)
                {
                    return new GetDeviceStatusResponse
                    {
                        Success = false,
                        Message = "设备未找到或离线",
                        ErrorCode = "DEVICE_NOT_FOUND",
                        DeviceId = request.DeviceId
                    };
                }

                var response = new GetDeviceStatusResponse
                {
                    Success = true,
                    Message = "获取设备状态成功",
                    DeviceId = request.DeviceId,
                    DeviceStatus = new DeviceStatus
                    {
                        DeviceId = device.DeviceId,
                        Online = device.IsConnected ?? false,
                        DoorStatus = "unknown", // 需要从设备实际查询
                        AlarmStatus = 0,
                        LastHeartbeat = ((DateTimeOffset)(device.LastHeartbeat ?? DateTime.MinValue)).ToUnixTimeSeconds(),
                        IpAddress = device.DeviceIP
                    }
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取设备状态异常: {request.DeviceId}");
                return new GetDeviceStatusResponse
                {
                    Success = false,
                    Message = $"获取设备状态异常: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        public override async Task<UpdateWhiteResponse> UpdateWhite(
            UpdateWhiteRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"接收更新白名单请求: DeviceId={request.DeviceId}, PersonNum={request.PersonNum}");

                var response = await EnqueueRequest<UpdateWhiteResponse>("UpdateWhite", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新白名单异常: {request.DeviceId}");
                return new UpdateWhiteResponse
                {
                    Success = false,
                    Message = $"更新白名单异常: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        // 可以继续实现其他gRPC方法...

        #endregion

        #region 健康检查和监控

        /// <summary>
        /// 获取服务健康状态
        /// </summary>
        public async Task<ServiceHealthStatus> GetHealthStatus()
        {
            try
            {
                var statistics = _deviceManager.GetStatistics();
                var queueLength = _requestQueue.Reader.CanCount ? _requestQueue.Reader.Count : -1;

                return new ServiceHealthStatus
                {
                    IsHealthy = true,
                    Message = "服务运行正常",
                    Statistics = statistics,
                    GrpcQueueLength = queueLength,
                    ConcurrentRequests = 50 - _concurrencyLimiter.CurrentCount,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ServiceHealthStatus
                {
                    IsHealthy = false,
                    Message = $"服务异常: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        #endregion

    }
}