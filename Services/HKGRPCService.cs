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

            // ����gRPC���������
            var queueOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _requestQueue = Channel.CreateBounded<GrpcRequestMessage>(queueOptions);

            // ���Ʋ�����������
            _concurrencyLimiter = new SemaphoreSlim(50); // ���50����������

            // ������������
            _ = Task.Run(ProcessGrpcRequests);
        }

        #region ���д���

        private async Task ProcessGrpcRequests()
        {
            _logger.LogInformation("gRPC������������");

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
                _logger.LogDebug($"����gRPC����: {request.RequestType}, DeviceId: {request.DeviceId}");

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
                    // ���������������
                    default:
                        request.CompletionSource.SetException(
                            new InvalidOperationException($"��֧�ֵ���������: {request.RequestType}"));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"����gRPC�����쳣: {request.RequestType}");
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
                throw new InvalidOperationException("gRPC��������ѹر�");
            }

            await _requestQueue.Writer.WriteAsync(request);
            var result = await request.CompletionSource.Task;
            return (T)result;
        }

        #endregion

        #region gRPC�ӿ�ʵ��

        public override async Task<OpenDoorResponse> OpenDoor(
            OpenDoorRequest request,
            ServerCallContext context)
        {
            try
            {
                _logger.LogInformation($"���տ�������: DeviceId={request.DeviceId}, Operator={request.Operator}");

                // ��֤�������
                if (string.IsNullOrEmpty(request.DeviceId))
                {
                    return new OpenDoorResponse
                    {
                        Success = false,
                        Message = "�豸ID����Ϊ��",
                        ErrorCode = "INVALID_DEVICE_ID",
                        DeviceId = request.DeviceId
                    };
                }

                // ͨ�����д�������
                var response = await EnqueueRequest<OpenDoorResponse>("OpenDoor", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"���Ų����쳣: {request.DeviceId}");
                return new OpenDoorResponse
                {
                    Success = false,
                    Message = $"���Ų����쳣: {ex.Message}",
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
                // ִ�п�������
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
                _logger.LogInformation($"������������: DeviceId={request.DeviceId}, Operator={request.Operator}");

                var response = await EnqueueRequest<RebootResponse>("Reboot", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"���������쳣: {request.DeviceId}");
                return new RebootResponse
                {
                    Success = false,
                    Message = $"���������쳣: {ex.Message}",
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
                _logger.LogInformation($"���ջ�ȡ�豸��Ϣ����: DeviceId={request.DeviceId}");

                var response = await EnqueueRequest<GetDeviceInfoResponse>("GetDeviceInfo", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"��ȡ�豸��Ϣ�쳣: {request.DeviceId}");
                return new GetDeviceInfoResponse
                {
                    Success = false,
                    Message = $"��ȡ�豸��Ϣ�쳣: {ex.Message}",
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

                // �������ִ�гɹ�������豸��Ϣ
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
                _logger.LogInformation($"���ջ�ȡ�豸״̬����: DeviceId={request.DeviceId}");

                // ֱ�Ӵ��豸��������ȡ״̬������Ҫ���д���
                var onlineDevices = _deviceManager.GetOnlineDevices();
                var device = onlineDevices.FirstOrDefault(d => d.DeviceId == request.DeviceId);

                if (device == null)
                {
                    return new GetDeviceStatusResponse
                    {
                        Success = false,
                        Message = "�豸δ�ҵ�������",
                        ErrorCode = "DEVICE_NOT_FOUND",
                        DeviceId = request.DeviceId
                    };
                }

                var response = new GetDeviceStatusResponse
                {
                    Success = true,
                    Message = "��ȡ�豸״̬�ɹ�",
                    DeviceId = request.DeviceId,
                    DeviceStatus = new DeviceStatus
                    {
                        DeviceId = device.DeviceId,
                        Online = device.IsConnected ?? false,
                        DoorStatus = "unknown", // ��Ҫ���豸ʵ�ʲ�ѯ
                        AlarmStatus = 0,
                        LastHeartbeat = ((DateTimeOffset)(device.LastHeartbeat ?? DateTime.MinValue)).ToUnixTimeSeconds(),
                        IpAddress = device.DeviceIP
                    }
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"��ȡ�豸״̬�쳣: {request.DeviceId}");
                return new GetDeviceStatusResponse
                {
                    Success = false,
                    Message = $"��ȡ�豸״̬�쳣: {ex.Message}",
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
                _logger.LogInformation($"���ո��°���������: DeviceId={request.DeviceId}, PersonNum={request.PersonNum}");

                var response = await EnqueueRequest<UpdateWhiteResponse>("UpdateWhite", request.DeviceId, request);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"���°������쳣: {request.DeviceId}");
                return new UpdateWhiteResponse
                {
                    Success = false,
                    Message = $"���°������쳣: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    DeviceId = request.DeviceId
                };
            }
        }

        // ���Լ���ʵ������gRPC����...

        #endregion

        #region �������ͼ��

        /// <summary>
        /// ��ȡ���񽡿�״̬
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
                    Message = "������������",
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
                    Message = $"�����쳣: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        #endregion

    }
}