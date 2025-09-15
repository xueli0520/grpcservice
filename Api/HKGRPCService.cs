using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService.Common;
using GrpcService.HKSDK;
using GrpcService.Infrastructure;
using GrpcService.Models;
using System.Text.Json;
using System.Xml.Serialization;
using StackExchange.Redis;

namespace GrpcService.Api
{
    public class HkDeviceService(
        ILogger<HkDeviceService> logger,
        DeviceManager deviceManager,
        IGrpcRequestQueueService requestQueue,
        IDeviceLoggerService deviceLogger,
        SubscribeEvent subscribeEvent,

        RedisService redis
        ) : HikDeviceService.HikDeviceServiceBase
    {
        private readonly ILogger<HkDeviceService> _logger = logger;
        private readonly DeviceManager _deviceManager = deviceManager;
        private readonly IGrpcRequestQueueService _requestQueue = requestQueue;
        private readonly IDeviceLoggerService _deviceLogger = deviceLogger;
        private readonly SubscribeEvent _bus = subscribeEvent;
        private readonly RedisService _redis = redis;

        /// <summary>
        /// 订阅推送事件（保持原实现）
        /// </summary>
        public override async Task SubscribeAllEvents(Empty request, IServerStreamWriter<DeviceEvent> responseStream, ServerCallContext context)
        {
            var clientId = context.GetHttpContext()?.Connection?.Id ?? Guid.NewGuid().ToString();
            var channel = $"device:events:{clientId}";
            var streamKey = $"device:events:stream";
            var consumerGroup = "device-events-group";
            var consumerName = $"consumer-{clientId}";

            _logger.LogInformation("客户端 {ClientId} 开始订阅事件频道 {Channel}", clientId, channel);
            try
            {
                try
                {
                    await _redis.SetStringAsync(streamKey, consumerGroup, null, true);
                    //await _redis.Subscribe(channel, async (ch, msg) =>
                    //{
                    //    var deviceEvent = JsonSerializer.Deserialize<DeviceEvent>(msg!);
                    //    if (deviceEvent != null)
                    //    {
                    //        await responseStream.WriteAsync(deviceEvent);
                    //    }
                    //});

                }
                catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
                {
                    _logger.LogWarning(ex, "Redis Consumer Group 已存在");
                }

                // 读取未处理的历史消息
                await _deviceManager.ProcessPendingMessages(streamKey, consumerGroup, consumerName, responseStream, context.CancellationToken);

                // 持续监听新消息
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var results = await _redis.StreamReadGroupAsync(
                            streamKey,
                            consumerGroup,
                            consumerName,
                            ">", // 只读取新消息
                            count: 10,
                            noAck: false);

                        if (results.Count != 0)
                        {
                            foreach (var result in results)
                            {
                                //foreach (var entry in result.Values)
                                //{
                                //    await _deviceManager.ProcessStreamEntry(entry, responseStream, streamKey, consumerGroup);
                                //}
                                await _deviceManager.ProcessStreamEntry(result, responseStream, streamKey, consumerGroup);
                            }
                        }
                        else
                        {
                            await Task.Delay(100, context.CancellationToken); // 无新消息时稍作等待
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "读取Redis Stream消息失败");
                        await Task.Delay(1000, context.CancellationToken); // 错误时等待更长时间
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("客户端 {ClientId} 订阅被取消", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅过程中发生错误");
                throw;
            }
            finally
            {
                try
                {
                    await _redis.StreamDeleteConsumerAsync(streamKey, consumerGroup, consumerName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理Redis消费者失败");
                }

                _logger.LogInformation("客户端 {ClientId} 已取消订阅", clientId);
            }
        }
        /// <summary>
        /// 获取服务信息（健康检查）
        /// </summary>
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
        /// 注册设备回调（保持原逻辑）
        /// </summary>
        public override Task<RegisterResponse> Register(RegisterRequest req, ServerCallContext ctx)
        {
            _deviceManager.RegisterEvent(req.DeviceId);
            return Task.FromResult(new RegisterResponse { Success = true, DeviceId = req.DeviceId });
        }

        /// <summary>
        /// 请注意：ExecuteIsapi 的泛型签名与原项目一致（返回 Task<T>），
        /// 我们把对它的调用放到 ExecuteIsapiWithConcurrency 中。
        /// </summary>
        public override Task<GetDeviceInfoResponse> GetDeviceInfo(GetDeviceInfoRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                var serializer = new XmlSerializer(typeof(GrpcDeviceInfo));
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "GET /ISAPI/System/deviceInfo", "GET", "",
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
            })!;
        }

        /// <summary>
        /// 远程开门
        /// </summary>
        public override Task<OpenDoorResponse> OpenDoor(OpenDoorRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/RemoteControl/door/1", "PUT",
                     ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AcsRemoteControlDoor.xml", new Dictionary<string, object> { { "cmd", "open" } }),
                     (ok, body) => new OpenDoorResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1001", DeviceId = req.DeviceId });
            })!;
        }

        /// <summary>
        /// 重启设备
        /// </summary>
        public override Task<RebootResponse> Reboot(RebootRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/System/reboot", "PUT", "",
                    (ok, body) => new RebootResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1002", DeviceId = req.DeviceId });
            })!;
        }

        /// <summary>
        /// 同步时间
        /// </summary>
        public override Task<SyncTimeResponse> SyncTime(SyncTimeRequest request, ServerCallContext context)
        {
            var currentTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssK");
            return _deviceManager.ExecuteIsapiWithConcurrency(request.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(request.DeviceId, "PUT /ISAPI/System/time", "PUT",
                    ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Basic\\SystemTime.xml", new Dictionary<string, object> { { "localTime", currentTime } }),
                    (ok, body) => new SyncTimeResponse
                    {
                        Success = ok,
                        Message = ok ? "OK" : body,
                        ErrorCode = ok ? "0" : "1003",
                        DeviceId = request.DeviceId,
                    });
            })!;
        }

        /// <summary>
        /// 获取设备版本
        /// </summary>
        public override Task<GetVersionResponse> GetVersion(GetVersionRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                var _ = await _deviceManager.Cms_SetConfigDevAsync(req.DeviceId, HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_GET_MODEL_ATTR, "InfoMgr", "DeviceVersion", "");
                return new GetVersionResponse { Success = true, Message = "OK", ErrorCode = "0" };
            })!;
        }

        /// <summary>
        /// 设置门禁模式
        /// </summary>
        public override Task<SetDoorModeResponse> SetDoorMode(SetDoorModeRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/DoorMode", "PUT",
                    $"<DoorMode><mode>{req.Mode}</mode></DoorMode>",
                    (ok, body) => new SetDoorModeResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1006", DeviceId = req.DeviceId });
            })!;
        }

        /// <summary>
        /// UpdateWhite (添加/更新人员、图片、卡号) - 保留原流程，并在每次 ExecuteIsapi 调用前后做并发控制
        /// </summary>
        public override Task<UpdateWhiteResponse> UpdateWhite(UpdateWhiteRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                OperationResponse whiteResult = new();
                UpdateWhiteResponse response = new();

                // 1. 添加人员
                await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json", "PUT", JsonSerializer.Serialize(new SetUpPersonRequest
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
                   (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!);

                if (whiteResult.StatusCode != 1)
                {
                    response = new UpdateWhiteResponse
                    {
                        Success = false,
                        Message = whiteResult.ErrorMsg,
                        ErrorCode = whiteResult.ErrorCode.ToString(),
                        DeviceId = req.DeviceId,
                        Users = req.Users
                    };
                    return response;
                }

                // 2. 添加人脸
                if (!string.IsNullOrEmpty(req.Users.PicPath))
                {
                    await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/Intelligent/FDLib/FDSetUp?format=json", "PUT", JsonSerializer.Serialize(new FaceInfoRequest
                    {
                        FaceURL = req.Users.PicPath,
                        FaceLibType = "blackFD",
                        FDID = "1",
                        FPID = req.Users.EmployeeNo,
                        FaceType = "normalFace",
                        SaveFacePic = true
                    }),
                       (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!);

                    if (whiteResult.StatusCode != 1)
                    {
                        response = new UpdateWhiteResponse
                        {
                            Success = false,
                            Message = whiteResult.ErrorMsg,
                            ErrorCode = whiteResult.ErrorCode.ToString(),
                            DeviceId = req.DeviceId,
                            Users = req.Users
                        };
                        return response;
                    }
                }

                // 3. 添加卡号
                if (!string.IsNullOrEmpty(req.Users.CardNumber) && req.Users.CardNumber != "0")
                {
                    await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json", "PUT", JsonSerializer.Serialize(new CardInfoRequest
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
                       (ok, body) => whiteResult = JsonSerializer.Deserialize<OperationResponse>(body)!);

                    response = new UpdateWhiteResponse
                    {
                        Success = whiteResult.StatusCode == 1,
                        Message = whiteResult.StatusCode == 1 ? "OK" : whiteResult.ErrorMsg,
                        ErrorCode = whiteResult.StatusCode == 1 ? "0" : whiteResult.ErrorCode.ToString(),
                        DeviceId = req.DeviceId,
                        Users = req.Users
                    };
                }

                // 4. 最终返回
                return response = new UpdateWhiteResponse { Success = true, DeviceId = req.DeviceId, Users = req.Users, Message = "OK", ErrorCode = "0" };
            })!;
        }

        /// <summary>
        /// DeleteWhite 操作（保留原逻辑，循环查询 DeleteProcess）
        /// </summary>
        public override async Task<DeleteWhiteResponse> DeleteWhite(DeleteWhiteRequest req, ServerCallContext ctx)
        {
            return await _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                // 1. 发起删除
                OperationResponse deleteResult = new();
                await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json", "PUT",
                    JsonSerializer.Serialize(new DeletaUserRequest
                    {
                        UserInfoDetail = new UserInfoDetailRequest
                        {
                            Mode = "byEmployeeNo",
                            EmployeeNoList = req.CustomIds.Select(id => new EmployeeNoList { EmployeeNo = id }).ToList()
                        }
                    }),
                    (ok, body) => deleteResult = JsonSerializer.Deserialize<OperationResponse>(body)!);

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

                // 2. Poll DeleteProcess
                var deleteProcessResult = new ProcessDeleteResponse();
                while (true)
                {
                    await _deviceManager.ExecuteIsapi(req.DeviceId,
                          "GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess?format=json",
                          "GET",
                          null,
                          (ok, body) => deleteProcessResult = JsonSerializer.Deserialize<ProcessDeleteResponse>(body)!);

                    if (deleteProcessResult?.UserInfoDetailDeleteProcess?.Status == "success")
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }

                return new DeleteWhiteResponse
                {
                    Success = true,
                    Message = "删除成功",
                    ErrorCode = "0",
                    DeviceId = req.DeviceId,
                    DeletedCount = req.CustomIds.Count,
                    TotalCount = req.CustomIds.Count
                };
            });
        }

        /// <summary>
        /// PageWhite（保留并用 ExecuteIsapiWithConcurrency 包裹）
        /// </summary>
        public override Task<PageWhiteResponse> PageWhite(PageWhiteRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "POST /ISAPI/AccessControl/UserInfo/Search?format=json", "POST", JsonSerializer.Serialize(new UserInfoSearchCond
                {
                    SearchID = req.MessageId,
                    SearchResultPosition = req.BeginNo,
                    MaxResults = req.PageSize
                }),
                    (ok, body) =>
                    {
                        var resp = new PageWhiteResponse
                        {
                            Success = ok,
                            Message = ok ? "OK" : body,
                            ErrorCode = ok ? "0" : "1011",
                            DeviceId = req.DeviceId
                        };
                        if (ok)
                        {
                            var userList = JsonSerializer.Deserialize<SdkWhiteResponse>(body)!.UserInfoSearch.UserInfo;
                            if (userList == null) return resp;
                            foreach (var user in userList)
                            {
                                CardInfoSearch? cardInfo = null;
                                _deviceManager.ExecuteIsapi(req.DeviceId, "POST /ISAPI/AccessControl/UserInfo/Search?format=json", "POST", JsonSerializer.Serialize(new UserInfoSearchCond
                                {
                                    SearchID = req.MessageId,
                                    SearchResultPosition = 0,
                                    MaxResults = 10,
                                    EmployeeNoList = new List<EmployeeNoList> { new EmployeeNoList { EmployeeNo = user.EmployeeNo } }
                                }),
                        (ok, body) => cardInfo = JsonSerializer.Deserialize<CardInfoSearch>(body)!);
                                resp.Users.Add(new WhiteUserInfo
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
                        return resp;
                    });
            })!;
        }

        /// <summary>
        /// UpdateTimezone
        /// </summary>
        public override Task<UpdateTimezoneResponse> UpdateTimezone(UpdateTimezoneRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "PUT /ISAPI/AccessControl/UserRightWeekPlanCfg/1?format=json", "PUT",
                   JsonSerializer.Serialize(req.TimezoneGroup),
                    (ok, body) =>
                    {
                        var response = JsonSerializer.Deserialize<OperationResponse>(body)!;
                        return new UpdateTimezoneResponse { Success = response.StatusCode == 1, Message = response.StatusString, ErrorCode = response.ErrorMsg, DeviceId = req.DeviceId };
                    });
            })!;
        }

        /// <summary>
        /// SetDoorTemplate
        /// </summary>
        public override Task<DoorTemplateResponse> SetDoorTemplate(DoorTemplateRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/AccessControl/DoorTemplate", "PUT",
                    $"<DoorTemplate><status>{req.Status}</status></DoorTemplate>",
                    (ok, body) => new DoorTemplateResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1015", DeviceId = req.DeviceId });
            })!;
        }

        /// <summary>
        /// SyncDeviceParameter
        /// </summary>
        public override Task<SyncDeviceParameterResponse> SyncDeviceParameter(SyncDeviceParameterRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "/ISAPI/System/deviceParameter", "PUT", null,
                    (ok, body) => new SyncDeviceParameterResponse { Success = ok, Message = ok ? "OK" : body, ErrorCode = ok ? "0" : "1016", DeviceId = req.DeviceId });
            })!;
        }

        /// <summary>
        /// GetWhiteUserTotal
        /// </summary>
        public override Task<GetWhiteUserTotalResponse> GetWhiteUserTotal(GetWhiteUserTotalRequest req, ServerCallContext ctx)
        {
            return _deviceManager.ExecuteIsapiWithConcurrency(req.DeviceId, async () =>
            {
                return await _deviceManager.ExecuteIsapi(req.DeviceId, "GET /ISAPI/AccessControl/UserInfo/Count?format=json", "GET", null,
                    (ok, body) => new GetWhiteUserTotalResponse()
                    {
                        Success = ok,
                        Message = ok ? "OK" : body,
                        ErrorCode = ok ? "0" : "1017",
                        DeviceId = req.DeviceId,
                        TotalCount = string.IsNullOrEmpty(body) ? 0 : JsonSerializer.Deserialize<GrpcUserInfo>(body)!.UserInfoCount!.UserNumber
                    });
            })!;
        }
    }
}
