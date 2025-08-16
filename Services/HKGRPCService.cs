using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService.Common;
using GrpcService.HKSDK;
using GrpcService.Models;
using System.Text.Json;
using System.Xml.Serialization;

namespace GrpcService.Services
{
    public class HkDeviceService(
        ILogger<HkDeviceService> logger,
        DeviceManager deviceManager,
        IGrpcRequestQueueService requestQueue,
        IDeviceLoggerService deviceLogger, SubscribeEvent subscribeEvent) : HikDeviceService.HikDeviceServiceBase
    {
        private readonly ILogger<HkDeviceService> _logger = logger;
        private readonly DeviceManager _deviceManager = deviceManager;
        private readonly IGrpcRequestQueueService _requestQueue = requestQueue;
        private readonly IDeviceLoggerService _deviceLogger = deviceLogger;
        private readonly SubscribeEvent _bus = subscribeEvent;
        /// <summary>
        /// 订阅推送事件
        /// </summary>
        /// <param name="req"></param>
        /// <param name="responseStream"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task SubscribeAllEvents(Empty req, IServerStreamWriter<DeviceEvent> responseStream, ServerCallContext context)
        {
            var reader = _bus.Subscribe();
            try
            {
                await foreach (var evt in reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(evt);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("grpc客户端取消订阅");
            }
        }
        /// <summary>
        /// 获取服务信息（健康检查）
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ServerInfoResponse> GetServerInfo(Empty request, ServerCallContext context)
        {
            var uptime = (long)(DateTime.UtcNow - DateTime.UtcNow).TotalSeconds;
            var response = new ServerInfoResponse
            {
                ServerName = "HikDevice gRPC Server",
                Version = "1.0.0",
                UptimeSeconds = uptime,
                Status = "OK"
            };

            return Task.FromResult(response);
        }
        /// <summary>
        /// 注册设备回调
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public override Task<RegisterResponse> Register(RegisterRequest req, ServerCallContext ctx)
        {
            _deviceManager.RegisterEvent(req.DeviceId);
            return Task.FromResult(new RegisterResponse { Success = true, DeviceId = req.DeviceId });
        }

        public override Task<GetDeviceInfoResponse> GetDeviceInfo(GetDeviceInfoRequest req, ServerCallContext ctx)
        {
            var serializer = new XmlSerializer(typeof(GrpcDeviceInfo));
            return _deviceManager.ExecuteIsapi(req.DeviceId, "GET /ISAPI/System/deviceInfo", "GET", "",
            (ok, body) =>
            {
                GrpcDeviceInfo device;
                using (var reader = new StringReader(body))
                {
                    device = (GrpcDeviceInfo)serializer.Deserialize(reader)!;
                }
                return new GetDeviceInfoResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1000",
                    DeviceInfo = new DeviceInfo
                    {
                        DeviceName = device.DeviceName,
                        FirmwareVersion = device.FirmwareVersion
                    },
                    DeviceId = req.DeviceId
                };
            });
        }


        /// <summary>
        /// 远程开门
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public override Task<OpenDoorResponse> OpenDoor(OpenDoorRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/RemoteControl/door/1", "PUT",
                ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AcsRemoteControlDoor.xml", new Dictionary<string, object> { { "cmd", "open" } }),
                (ok, body) => new OpenDoorResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1001", DeviceId = req.DeviceId });
        /// <summary>
        /// 重启设备
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public override Task<RebootResponse> Reboot(RebootRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/System/reboot", "PUT", "",
                (ok, body) => new RebootResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1002", DeviceId = req.DeviceId });
        /// <summary>
        /// 同步时间
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<SyncTimeResponse> SyncTime(SyncTimeRequest request, ServerCallContext context)
        {
            var currentTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");
            return await _deviceManager.ExecuteIsapi(request.DeviceId, "PUT /ISAPI/System/time", "PUT", ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Basic\\SystemTime.xml", new Dictionary<string, object> { { "localTime", currentTime } }),
                (ok, body) => new SyncTimeResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1003",
                    DeviceId = request.DeviceId,
                });
        }
        /// <summary>
        /// 获取设备信息
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public override Task<GetVersionResponse> GetVersion(GetVersionRequest req, ServerCallContext ctx)
        {
            var result = _deviceManager.Cms_SetConfigDevAsync(req.DeviceId, HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_GET_MODEL_ATTR, "InfoMgr", "DeviceVersion", "");
            return Task.FromResult(new GetVersionResponse { Success = true });
        }
        /// <summary>
        /// 设置门禁模式
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public override Task<SetDoorModeResponse> SetDoorMode(SetDoorModeRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/DoorMode", "PUT",
                $"<DoorMode><mode>{req.Mode}</mode></DoorMode>",
                (ok, body) => new SetDoorModeResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1006", DeviceId = req.DeviceId });
        public override Task<UpdateUserAllResponse> UpdateUserAll(UpdateUserAllRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/UserInfo/Record", "PUT",
                BuildUserXml(req.Users),
                (ok, body) => new UpdateUserAllResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1007",
                    DeviceId = req.DeviceId,
                    UpdatedCount = ok ? req.Users.Count : 0,
                    TotalCount = req.Users.Count
                });

        public override Task<GetUserListResponse> GetUserList(GetUserListRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, $"/ISAPI/AccessControl/UserInfo/Record?pageNo={req.PageNumber}&pageSize={req.PageSize}", "GET", null,
                (ok, body) => new GetUserListResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1008",
                    DeviceId = req.DeviceId,
                    TotalCount = 0
                });

        public override Task<UpdateWhiteResponse> UpdateWhite(UpdateWhiteRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/WhiteList/Record", "PUT",
                BuildWhiteListXml(req.Users),
                (ok, body) => new UpdateWhiteResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1009",
                    DeviceId = req.DeviceId,
                    UpdatedCount = ok ? req.Users.Count : 0,
                    TotalCount = req.Users.Count
                });

        public override Task<DeleteWhiteResponse> DeleteWhite(DeleteWhiteRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/WhiteList/Record", "DELETE",
                BuildDeleteWhiteXml(req.CustomIds),
                (ok, body) => new DeleteWhiteResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1010",
                    DeviceId = req.DeviceId,
                    DeletedCount = ok ? req.CustomIds.Count : 0,
                    TotalCount = req.CustomIds.Count
                });

        public override Task<PageWhiteResponse> PageWhite(PageWhiteRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, $"/ISAPI/AccessControl/WhiteList/Record?beginNo={req.BeginNo}&pageSize={req.PageSize}", "GET", null,
                (ok, body) => new PageWhiteResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1011",
                    DeviceId = req.DeviceId,
                    TotalCount = 0
                });

        public override Task<DetailWhiteResponse> DetailWhite(DetailWhiteRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, $"/ISAPI/AccessControl/WhiteList/Record?code={req.Code}", "GET", null,
                (ok, body) => new DetailWhiteResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1012",
                    DeviceId = req.DeviceId
                });

        public override Task<UpdateTimezoneResponse> UpdateTimezone(UpdateTimezoneRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/TimeZone", "PUT",
                BuildTimezoneXml(req.TimezoneGroup),
                (ok, body) => new UpdateTimezoneResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1013", DeviceId = req.DeviceId });

        public override Task<QueryTimezoneResponse> QueryTimezone(QueryTimezoneRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/TimeZone", "GET", null,
                (ok, body) => new QueryTimezoneResponse
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1014",
                    DeviceId = req.DeviceId
                });

        public override Task<DoorTemplateResponse> SetDoorTemplate(DoorTemplateRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/DoorTemplate", "PUT",
                $"<DoorTemplate><status>{req.Status}</status></DoorTemplate>",
                (ok, body) => new DoorTemplateResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1015", DeviceId = req.DeviceId });

        public override Task<SyncDeviceParameterResponse> SyncDeviceParameter(SyncDeviceParameterRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/System/deviceParameter", "PUT", null,
                (ok, body) => new SyncDeviceParameterResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1016", DeviceId = req.DeviceId });

        public override Task<GetWhiteUserTotalResponse> GetWhiteUserTotal(GetWhiteUserTotalRequest req, ServerCallContext ctx) =>
            _deviceManager.ExecuteIsapi(req.DeviceId, "GET /ISAPI/AccessControl/UserInfo/Count", "GET", null,
                (ok, body) => new GetWhiteUserTotalResponse()
                {
                    Success = ok,
                    Message = ok ? "OK" : body,
                    ErrorCode = ok ? "0" : "1017",
                    DeviceId = req.DeviceId,
                    TotalCount = string.IsNullOrEmpty(body) ? 0 : JsonSerializer.Deserialize<GrpcUserInfo>(body)!.UserInfoCount!.userNumber
                });
        // 辅助方法：构建用户XML
        private string BuildUserXml(Google.Protobuf.Collections.RepeatedField<UserInfo> users)
        {
            var xml = "<UserInfoList>";
            foreach (var user in users)
            {
                xml += $"<UserInfo><userId>{user.UserId}</userId><name>{user.Name}</name><cardNumber>{user.CardNumber}</cardNumber></UserInfo>";
            }
            xml += "</UserInfoList>";
            return xml;
        }

        // 辅助方法：构建白名单XML
        private string BuildWhiteListXml(Google.Protobuf.Collections.RepeatedField<WhiteUserInfo> users)
        {
            var xml = "<WhiteList>";
            foreach (var user in users)
            {
                xml += $"<WhiteUser><customId>{user.CustomId}</customId><name>{user.Name}</name></WhiteUser>";
            }
            xml += "</WhiteList>";
            return xml;
        }

        // 辅助方法：构建删除白名单XML
        private string BuildDeleteWhiteXml(Google.Protobuf.Collections.RepeatedField<string> customIds)
        {
            var xml = "<DeleteWhiteList>";
            foreach (var id in customIds)
            {
                xml += $"<customId>{id}</customId>";
            }
            xml += "</DeleteWhiteList>";
            return xml;
        }

        // 辅助方法：构建时段XML
        private string BuildTimezoneXml(TimezoneGroup timezoneGroup)
        {
            var xml = $"<TimeZone><strategyId>{timezoneGroup.StrategyId}</strategyId><strategyName>{timezoneGroup.StrategyName}</strategyName></TimeZone>";
            return xml;
        }
    }
}