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

        public override Task<UpdateWhiteResponse> UpdateWhite(UpdateWhiteRequest req, ServerCallContext ctx)
        {
            OperationResponse whiteResult = new();
            //1.先添加人员
            _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserInfo/SetUp", "PUT", JsonSerializer.Serialize(new SetUpPersonRequest
            {
                UserInfo = new UserInfo
                {
                    EmployeeNo = req.Users.EmployeeNo,
                    Name = req.Users.Name,
                    Password = req.Users.DevPassword,
                    UserType = req.Users.UserType,
                    Valid = new ValidInfo
                    {
                        BeginTime = req.Users.StartOn,
                        EndTime = req.Users.EndOn,
                        Enable = true
                    },
                }
            }),
               (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!
                   );
            if (whiteResult.StatusCode != 1)
            {
                return Task.FromResult(new UpdateWhiteResponse
                {
                    Success = false,
                    Message = whiteResult.ErrorMsg,
                    ErrorCode = whiteResult.ErrorCode.ToString(),
                    DeviceId = req.DeviceId,
                    Users = req.Users
                });
            }
            //2.添加人脸    
            _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/Intelligent/FDLib/FDSetUp", "PUT", JsonSerializer.Serialize(new FaceInfoRequest
            {
                FaceURL = req.Users.PicPath,
                FaceLibType = "blackFD",
                FDID = req.Users.EmployeeNo,
                FPID = req.Users.EmployeeNo,
                FaceType = "normalFace",
                SaveFacePic = true
            }),
               (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!
                   );
            if (whiteResult.StatusCode != 1)
            {
                return Task.FromResult(new UpdateWhiteResponse
                {
                    Success = false,
                    Message = whiteResult.ErrorMsg,
                    ErrorCode = whiteResult.ErrorCode.ToString(),
                    DeviceId = req.DeviceId,
                    Users = req.Users
                });
            }
            //3.添加卡号
            _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/CardInfo/SetUp", "PUT", JsonSerializer.Serialize(new CardInfoRequest
            {
                CardInfo = new CardInfo
                {
                    CardNo = req.Users.CardNumber,
                    EmployeeNo = req.Users.EmployeeNo,
                    CardType = "normalCard",
                    CheckEmployeeNo = true,
                    CheckCardNo = true
                }
            }),
               (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!
                   );
            return Task.FromResult(new UpdateWhiteResponse
            {
                Success = whiteResult.StatusCode == 1,
                Message = whiteResult.ErrorMsg,
                ErrorCode = whiteResult.ErrorCode.ToString(),
                DeviceId = req.DeviceId,
                Users = req.Users
            });
        }

        public override async Task<DeleteWhiteResponse> DeleteWhite(DeleteWhiteRequest req, ServerCallContext ctx)
        {
            // 1. 先发起删除请求
            OperationResponse deleteResult = new();
            await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserInfoDetail/Delete", "PUT", JsonSerializer.Serialize(
                 new DeletaUserRequest
                 {
                     UserInfoDetail = new UserInfoDetailRequest
                     {
                         Mode = "byEmployeeNo",
                         EmployeeNoList = [.. req.CustomIds.Select(id => new EmployeeNoList { EmployeeNo = id })]
                     }
                 }),
                 (ok, body) => deleteResult = JsonSerializer.Deserialize<OperationResponse>(body)!);

            // 2. 如果设备没返回成功，直接结束
            if (deleteResult.StatusCode != 1)
            {
                return new DeleteWhiteResponse
                {
                    Success = false,
                    Message = deleteResult.ErrorMsg,
                    ErrorCode = deleteResult.ErrorCode.ToString(),
                    DeviceId = req.DeviceId,
                    DeletedCount = 0,
                    TotalCount = req.CustomIds.Count
                };
            }

            // 3. 循环检查 DeleteProcess，直到返回 success
            var deleteProcessResult = new ProcessDeleteResponse();
            while (true)
            {
                await _deviceManager.ExecuteIsapi(req.DeviceId,
                      "GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess",
                      "GET",
                      null,
                      (ok, body) => deleteProcessResult = JsonSerializer.Deserialize<ProcessDeleteResponse>(body)!);

                if (deleteProcessResult?.UserInfoDetailDeleteProcess?.Status == "success")
                {
                    break;
                }
                // 延迟 1s 再重试，避免打爆设备
                await Task.Delay(1000);
            }

            // 4. 返回最终结果
            return new DeleteWhiteResponse
            {
                Success = true,
                Message = "删除成功",
                ErrorCode = "0",
                DeviceId = req.DeviceId,
                DeletedCount = req.CustomIds.Count,
                TotalCount = req.CustomIds.Count
            };
        }
        public override Task<PageWhiteResponse> PageWhite(PageWhiteRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "POST /ISAPI/AccessControl/UserInfo/Search", "POST", JsonSerializer.Serialize(new UserInfoSearchCond
            {
                SearchID = req.MessageId,
                SearchResultPosition = req.BeginNo,
                MaxResults = req.PageSize
            }),
                (ok, body) =>
                {
                    var result = new PageWhiteResponse
                    {
                        Success = ok,
                        Message = ok ? "OK" : body,
                        ErrorCode = ok ? "0" : "1011",
                        DeviceId = req.DeviceId
                    };
                    if (ok)
                    {
                        var userList = JsonSerializer.Deserialize<SdkWhiteResponse>(body)!.UserInfoSearch.UserInfo;
                        if (userList == null) return result;
                        foreach (var user in userList)
                        {
                            CardInfoSearch? cardInfo = null;
                            _deviceManager.ExecuteIsapi(req.DeviceId, "POST /ISAPI/AccessControl/UserInfo/Search", "POST", JsonSerializer.Serialize(new UserInfoSearchCond
                            {
                                SearchID = req.MessageId,
                                SearchResultPosition = 0,
                                MaxResults = 10,
                                EmployeeNoList = [new() { EmployeeNo = user.EmployeeNo }]
                            }),
                (ok, body) => cardInfo = JsonSerializer.Deserialize<CardInfoSearch>(body)!);
                            result.Users.Add(new WhiteUserInfo
                            {
                                EmployeeNo = user.EmployeeNo,
                                Name = user.Name,
                                DevPassword = user.Password,
                                StartOn = user.Valid?.BeginTime,
                                EndOn = user.Valid?.EndTime,
                                PicPath = user.FaceURL,
                                CardNumber = cardInfo?.CardInfo?.Count > 0 ? cardInfo.CardInfo[0].CardNo : ""
                            });
                        }

                    }
                    return result;
                });

        public override Task<UpdateTimezoneResponse> UpdateTimezone(UpdateTimezoneRequest req, ServerCallContext ctx)
            => _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserRightWeekPlanCfg/1?format=json", "PUT",
               JsonSerializer.Serialize(req.TimezoneGroup),
                (ok, body) =>
                {
                    var response = JsonSerializer.Deserialize<OperationResponse>(body)!;
                    return new UpdateTimezoneResponse { Success = response.StatusCode == 1, Message = response.StatusString, ErrorCode = response.ErrorMsg, DeviceId = req.DeviceId };
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
                    TotalCount = string.IsNullOrEmpty(body) ? 0 : JsonSerializer.Deserialize<GrpcUserInfo>(body)!.UserInfoCount!.UserNumber
                });

    }
}