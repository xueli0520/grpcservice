using Grpc.Core;
using GrpcService.Common;
using GrpcService.HKSDK;
using GrpcService.Models;
using System.Text.Json;
using System.Threading.Channels;

namespace GrpcService.Services
{
    public class HkDeviceService : HikDeviceService.HikDeviceServiceBase
    {
        private readonly ILogger<HkDeviceService> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly IGrpcRequestQueueService _requestQueue;
        private readonly IDeviceLoggerService _deviceLogger;

        public HkDeviceService(
            ILogger<HkDeviceService> logger,
            DeviceManager deviceManager,
            IGrpcRequestQueueService requestQueue,
            IDeviceLoggerService deviceLogger)
        {
            _logger = logger;
            _deviceManager = deviceManager;
            _requestQueue = requestQueue;
            _deviceLogger = deviceLogger;
        }

        public override async Task<OpenDoorResponse> OpenDoor(OpenDoorRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "OpenDoor",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到开门请求: Operator={Operator}, MessageId={MessageId}",
                        req.Operator, req.MessageId);

                    var parameters = new Dictionary<string, object>();
                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "opendoor", parameters, ct);

                    return new OpenDoorResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1001",
                        DeviceId = req.DeviceId
                    };
                },
                context.CancellationToken);
        }

        public override async Task<RebootResponse> Reboot(RebootRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "Reboot",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到重启请求: Operator={Operator}, MessageId={MessageId}",
                        req.Operator, req.MessageId);

                    var parameters = new Dictionary<string, object>();
                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "reboot", parameters, ct);

                    return new RebootResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1002",
                        DeviceId = req.DeviceId
                    };
                },
                context.CancellationToken);
        }

        public override async Task<SyncTimeResponse> SyncTime(SyncTimeRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "SyncTime",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到同步时间请求: Timestamp={Timestamp}, Operator={Operator}",
                        req.Timestamp, req.Operator);

                    var parameters = new Dictionary<string, object>
                    {
                        ["timestamp"] = req.Timestamp
                    };

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "synctime", parameters, ct);

                    return new SyncTimeResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1003",
                        DeviceId = req.DeviceId
                    };
                },
                context.CancellationToken);
        }

        public override async Task<GetDeviceInfoResponse> GetDeviceInfo(GetDeviceInfoRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "GetDeviceInfo",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到获取设备信息请求: Operator={Operator}, MessageId={MessageId}",
                        req.Operator, req.MessageId);

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "getdeviceinfo", new Dictionary<string, object>(), ct);

                    var response = new GetDeviceInfoResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1004",
                        DeviceId = req.DeviceId
                    };

                    if (result.Success && result.ResultData != null)
                    {
                        response.DeviceInfo = new DeviceInfo
                        {
                            DeviceId = result.ResultData.GetValueOrDefault("device_id", req.DeviceId).ToString()!,
                            IpAddress = result.ResultData.GetValueOrDefault("device_ip", "未知").ToString()!,
                            LastOnlineTime = DateTimeOffset.Parse(
                                result.ResultData.GetValueOrDefault("last_online_time", DateTime.Now.ToString()).ToString()!)
                                .ToUnixTimeSeconds()
                        };
                    }

                    return response;
                },
                context.CancellationToken);
        }

        public override async Task<GetDeviceStatusResponse> GetDeviceStatus(GetDeviceStatusRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "GetDeviceStatus",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到获取设备状态请求: Operator={Operator}",
                        req.Operator);

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "getstatus", new Dictionary<string, object>(), ct);

                    var response = new GetDeviceStatusResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1005",
                        DeviceId = req.DeviceId
                    };

                    if (result.Success && result.ResultData != null)
                    {
                        response.DeviceStatus = new DeviceStatus
                        {
                            DeviceId = req.DeviceId,
                            Online = result.ResultData.GetValueOrDefault("status").ToString() == "online",
                            DoorStatus = "unknown", // 需要根据实际情况设置
                            AlarmStatus = 0,
                            LastHeartbeat = DateTimeOffset.Parse(
                                result.ResultData.GetValueOrDefault("last_heartbeat", DateTime.Now.ToString()).ToString()!)
                                .ToUnixTimeSeconds(),
                            IpAddress = result.ResultData.GetValueOrDefault("device_ip", "未知").ToString()!
                        };
                    }

                    return response;
                },
                context.CancellationToken);
        }

        public override async Task<SetDoorModeResponse> SetDoorMode(SetDoorModeRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "SetDoorMode",
                request,
                async (req, ct) =>
                {
                    _deviceLogger.LogDeviceInfo(req.DeviceId, "收到设置门禁模式请求: Mode={Mode}, Operator={Operator}",
                        req.Mode, req.Operator);

                    var parameters = new Dictionary<string, object>
                    {
                        ["mode"] = req.Mode
                    };

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "setdoormode", parameters, ct);

                    return new SetDoorModeResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1006",
                        DeviceId = req.DeviceId
                    };
                },
                context.CancellationToken);
        }

        // 实现其他接口方法...
        public override async Task<UpdateUserAllResponse> UpdateUserAll(UpdateUserAllRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "UpdateUserAll",
                request,
                async (req, ct) =>
                {
                    var parameters = new Dictionary<string, object>
                    {
                        ["users"] = req.Users
                    };

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "updateuserall", parameters, ct);

                    return new UpdateUserAllResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1007",
                        DeviceId = req.DeviceId,
                        UpdatedCount = result.ResultData?.GetValueOrDefault("updated_count", 0).ToType<int>() ?? 0,
                        TotalCount = result.ResultData?.GetValueOrDefault("total_count", req.Users.Count).ToType<int>() ?? req.Users.Count
                    };
                },
                context.CancellationToken);
        }

        public override async Task<GetUserListResponse> GetUserList(GetUserListRequest request, ServerCallContext context)
        {
            return await _requestQueue.EnqueueRequestAsync(
                request.DeviceId,
                "GetUserList",
                request,
                async (req, ct) =>
                {
                    var parameters = new Dictionary<string, object>
                    {
                        ["page_number"] = req.PageNumber,
                        ["page_size"] = req.PageSize
                    };

                    var result = await _deviceManager.ExecuteDeviceCommandAsync(
                        req.DeviceId, "getuserlist", parameters, ct);

                    var response = new GetUserListResponse
                    {
                        Success = result.Success,
                        Message = result.Message,
                        ErrorCode = result.Success ? "0" : "1008",
                        DeviceId = req.DeviceId,
                        TotalCount = result.ResultData?.GetValueOrDefault("total_count", 0).ToType<int>() ?? 0
                    };

                    // 这里需要根据实际返回数据构建用户列表
                    return response;
                },
                context.CancellationToken);
        }

        // ... 继续实现其他方法
    }
}