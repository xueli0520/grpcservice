using GrpcService.Common;
using GrpcService.HKSDK;
using GrpcService.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static GrpcService.HKSDK.HCISUPCMS;
using static GrpcService.HKSDK.HCISUPAlarm;
using static GrpcService.HKSDK.HCISUPPublic;
using Microsoft.Graph.Models;
using System.Text.Json;

namespace GrpcService.Infrastructure
{
    public class CMSService : IHostedService, IDisposable
    {
        private readonly DeviceManager _deviceManager;
        private readonly ILogger<CMSService> _logger;
        private readonly IDeviceLoggerService _deviceLogger;
        private readonly HikDeviceConfiguration _config;
        private readonly object _initLock = new();
        private readonly object _disposeLock = new();
        private bool _isInitialized = false;
        private bool _disposed = false;

        // 常量提取
        private const int MaxIpLength = 127;
        private const int MaxBucketLength = 63;
        private const int MaxRegionLength = 31;
        private const string DefaultTopicFilter = "model/event/report/#\r\n";
        private const string SdkLogDir = "SdkLog";

        public CMSService(
            ILogger<CMSService> logger,
            DeviceManager deviceManager,
            IDeviceLoggerService deviceLogger,
            IOptions<HikDeviceConfiguration> config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _deviceLogger = deviceLogger ?? throw new ArgumentNullException(nameof(deviceLogger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

            // 配置校验
            ValidateConfiguration();

            CommonMethod.InitializeLogger(_logger);
            //Initialize();
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_config.CmsServerIP))
                throw new ArgumentException("CmsServerIP 配置不能为空");
            if (_config.CmsServerPort <= 0)
                throw new ArgumentException("CmsServerPort 配置无效");
            if (string.IsNullOrWhiteSpace(_config.AlarmServerIP))
                throw new ArgumentException("AlarmServerIP 配置不能为空");
            if (_config.AlarmServerPort <= 0)
                throw new ArgumentException("AlarmServerPort 配置不能为空");
            if (string.IsNullOrWhiteSpace(_config.DasServerIP))
                throw new ArgumentException("DasServerIP 配置不能为空");
            if (_config.DasServerPort <= 0)
                throw new ArgumentException("DasServerPort 配置无效");
            if (_config.Storage == null)
                throw new ArgumentException("Storage 配置不能为空");
        }


        // 实现IHostedService接口
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在启动CMSService...");

            // 在后台线程中初始化，避免阻塞启动
            await Task.Run(() => Initialize(), cancellationToken);

            _logger.LogInformation("CMSService启动完成");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止CMSService...");

            await Task.Run(() =>
            {
                StopListen();
            }, cancellationToken);

            _logger.LogInformation("CMSService停止完成");
        }

        /// <summary>
        /// 初始化CMS服务
        /// </summary>
        private void Initialize()
        {
            lock (_initLock)
            {
                if (_isInitialized) return;

                try
                {
                    _logger.LogInformation("开始初始化服务...");
                    _logger.LogDebug("当前平台: {Platform}", GetPlatformInfo());
                    SetupDependencyLibraries();
                    // 初始化SDK
                    if (!NET_ECMS_Init())
                    {
                        var errorCode = NET_ECMS_GetLastError();
                        var errorMessage = $"NET_ECMS_Init failed, error: {errorCode}";
                        _logger.LogError(errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }
                    if (!NET_EALARM_Init())
                    {
                        var errorCode = NET_EALARM_GetLastError();
                        var errorMessage = $"NET_EALARM_Init failed, error: {errorCode}";
                        _logger.LogError(errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }

                    //配置设备心跳 
                    IntPtr pBuffer = IntPtr.Zero;
                    pBuffer = Marshal.AllocHGlobal(sizeof(bool));
                    Marshal.WriteByte(pBuffer, 1); // TRUE = 1
                    if (!NET_ECMS_SetSDKLocalCfg(NET_EHOME_LOCAL_CFG_DEV_DAS_PINGREQ_CALLBACK, pBuffer))
                    {
                        var errorCode = NET_ECMS_GetLastError();
                        var errorMessage = $"NET_ECMS_Init failed, error: {errorCode}";
                        _logger.LogError(errorMessage);
                        Console.WriteLine(errorMessage);
                    }
                    _logger.LogInformation("心跳设置 成功");
                    _logger.LogInformation("初始化成功!");

                    // 设置日志
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SdkLogDir);
                    CommonMethod.EnsureDirectoryExists(logPath);
                    NET_ECMS_SetLogToFile(3, logPath + "/cms", false);
                    NET_EALARM_SetLogToFile(3, logPath + "/alarm", false);
                    if (!NET_EALARM_SetSDKLocalCfg(NET_EHOME_LOCAL_CFG_TYPE.COM_PATH, Marshal.StringToHGlobalAnsi(CMSServiceHelpers.sCurPath + "/HCAapSDKCom")))
                    {
                        _logger.LogError("NET_EALARM_SetSDKLocalCfg COM_PATH failed, error:" + NET_EALARM_GetLastError());
                    }

                    // 报警服务启动监听
                    Alarm_Startlisten();

                    // 订阅存储消息
                    //SubscribeStorageMessages();

                    // 中心服务启动监听
                    StartListen();

                    _isInitialized = true;
                    _logger.LogInformation("服务初始化完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "服务初始化失败");
                    throw;
                }
            }
        }


        /// <summary>
        /// 订阅消息
        /// </summary>
        private void Alarm_Startlisten()
        {
            IntPtr ptrSubscribeMsgParam = IntPtr.Zero;
            try
            {
                CMSServiceHelpers.struAlarmListenParam.Init();

                var alarmServerIP = _config.AlarmServerIP;
                CMSServiceHelpers.struAlarmListenParam.byProtocolType = 2; // 2-MQTT(ISUP5.0)
                CMSServiceHelpers.struAlarmListenParam.struAddress.wPort = (short)_config.AlarmServerPort;
                CMSServiceHelpers.struAlarmListenParam.dwKeepAliveSec = _config.HeartbeatCheckIntervalSeconds;
                CMSServiceHelpers.struAlarmListenParam.dwTimeOutCount = _config.CommandTimeoutMinutes;
                alarmServerIP.CopyTo(0, CMSServiceHelpers.struAlarmListenParam.struAddress.szIP, 0, alarmServerIP.Length);
                CMSServiceHelpers.AlarmMsgCallBack_Func = new EHomeMsgCallBack(AlarmMsgCallBack);
                CMSServiceHelpers.struAlarmListenParam.fnMsgCb = CMSServiceHelpers.AlarmMsgCallBack_Func;

                CMSServiceHelpers.AlarmListenHandle = NET_EALARM_StartListen(ref CMSServiceHelpers.struAlarmListenParam);
                if (CMSServiceHelpers.AlarmListenHandle < 0)
                {
                    _logger.LogError($"NET_EALARM_StartListen failed, error:{NET_ECMS_GetLastError()}");
                }
                _logger.LogInformation($"报警服务启动成功,ip:{alarmServerIP},端口:{_config.AlarmServerPort},handle:{CMSServiceHelpers.AlarmListenHandle}");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "报警服务启动异常");
            }

        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed) return;

                try
                {
                    _logger.LogInformation("开始清理CMS服务资源...");

                    // 停止监听
                    StopListen();

                    // 清理SDK资源
                    if (_isInitialized)
                    {
                        try
                        {
                            if (!NET_ECMS_Fini())
                            {
                                var errorCode = NET_ECMS_GetLastError();
                                _logger.LogError("NET_ECMS_Fini failed, error: {ErrorCode}", errorCode);
                            }
                            else
                            {
                                _logger.LogInformation("NET_ECMS_Fini 成功");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "清理SDK异常");
                        }
                    }

                    _disposed = true;
                    _logger.LogInformation("CMS服务资源清理完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理CMS服务资源时发生异常");
                }
            }
        }

        ~CMSService()
        {
            Dispose(false);
        }

        /// <summary>
        /// 启动监听
        /// </summary>
        private void StartListen()
        {
            try
            {
                CMSServiceHelpers.cmsListenParam.struAddress.Init();

                var cmsServerIP = _config.CmsServerIP;
                cmsServerIP.CopyTo(0, CMSServiceHelpers.cmsListenParam.struAddress.szIP, 0,
                    Math.Min(cmsServerIP.Length, MaxIpLength));

                CMSServiceHelpers.cmsListenParam.struAddress.wPort = (short)_config.CmsServerPort;

                CMSServiceHelpers.ISUP_REGISTER_Func = new DEVICE_REGISTER_CB(FRegisterCallBack);
                CMSServiceHelpers.cmsListenParam.fnCB = CMSServiceHelpers.ISUP_REGISTER_Func;
                CMSServiceHelpers.cmsListenParam.pUserData = IntPtr.Zero;

                CMSServiceHelpers.CmsListenHandle = NET_ECMS_StartListen(ref CMSServiceHelpers.cmsListenParam);
                if (CMSServiceHelpers.CmsListenHandle < 0)
                {
                    var errorCode = NET_ECMS_GetLastError();
                    var errorMessage = $"NET_ECMS_StartListen failed, error: {errorCode}";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogInformation("中心服务启动成功, IP: {IP}, Port: {Port}, Handle: {Handle}",
                    cmsServerIP, _config.CmsServerPort, CMSServiceHelpers.CmsListenHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监听失败");
                throw;
            }
        }

        /// <summary>
        /// 设备注册回调
        /// </summary>
        public bool FRegisterCallBack(int lUserID, int dwDataType, IntPtr pOutBuffer, uint dwOutLen, IntPtr pInBuffer, uint dwInLen, IntPtr pUserData)
        {
            try
            {
                _logger.LogDebug("FRegisterCallBack, dwDataType: {DataType}, lUserID: {UserID}", dwDataType, lUserID);
                NET_EHOME_DEV_REG_INFO_V12 struDevInfo = new();
                struDevInfo.Init();
                if (pOutBuffer != IntPtr.Zero)
                {
                    struDevInfo = (NET_EHOME_DEV_REG_INFO_V12)Marshal.PtrToStructure(pOutBuffer, typeof(NET_EHOME_DEV_REG_INFO_V12))!;
                }

                string strDeviceID = Encoding.Default.GetString(struDevInfo.struRegInfo.byDeviceID).TrimEnd('\0');

                switch (dwDataType)
                {
                    case ENUM_DEV_ON:
                    case ENUM_DEV_ADDRESS_CHANGED:
                    case ENUM_DEV_DAS_REREGISTER:
                        HandleDeviceOnline(lUserID, strDeviceID, struDevInfo, pInBuffer, dwInLen);
                        break;
                    case ENUM_DEV_DAS_PINGREO:
                        HandleDeviceHeartbeat(lUserID, strDeviceID);
                        break;
                    case ENUM_DEV_OFF:
                        HandleDeviceOffline(lUserID, strDeviceID);
                        break;
                    case ENUM_DEV_AUTH:
                        HandleDeviceAuth(strDeviceID, pInBuffer);
                        break;
                    case ENUM_DEV_DAS_REQ:
                        HandleDasRequest(pInBuffer);
                        break;
                    case ENUM_DEV_SESSIONKEY:
                        HCISUPPublic.NET_EHOME_DEV_SESSIONKEY devSessionkey = new();
                        devSessionkey.Init();
                        struDevInfo.struRegInfo.byDeviceID.CopyTo(devSessionkey.sDeviceID, 0);
                        struDevInfo.struRegInfo.bySessionKey.CopyTo(devSessionkey.sSessionKey, 0);

                        NET_ECMS_SetDeviceSessionKey(ref devSessionkey);
                        NET_EALARM_SetDeviceSessionKey(ref devSessionkey);
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备注册回调处理异常, UserID: {UserID}, DataType: {DataType}", lUserID, dwDataType);
                return false;
            }
        }

        /// <summary>
        /// 处理设备上线
        /// </summary>
        private bool HandleDeviceOnline(int lUserID, string deviceId, NET_EHOME_DEV_REG_INFO_V12 struDevInfo, IntPtr pInBuffer, uint dwInLen)
        {
            try
            {
                _deviceLogger.LogDeviceInfo(deviceId, "设备上线: UserID: {UserID}", lUserID);
                // 设置服务器心跳参数
                var struServerInfo = Marshal.PtrToStructure<NET_EHOME_SERVER_INFO_V50>(pInBuffer)!;
                //AMS服务地址和端口下发
                struServerInfo.dwAlarmServerType = 2;
                string AlarmServerIP = _config.AlarmServerIP;
                AlarmServerIP.CopyTo(0, struServerInfo.struTCPAlarmSever.szIP, 0, AlarmServerIP.Length);
                struServerInfo.struTCPAlarmSever.wPort = short.Parse(_config.AlarmServerPort.ToString());
                AlarmServerIP.CopyTo(0, struServerInfo.struUDPAlarmSever.szIP, 0, AlarmServerIP.Length);
                struServerInfo.struUDPAlarmSever.wPort = short.Parse(_config.AlarmServerPort.ToString());

                // CMS 心跳配置
                struServerInfo.dwKeepAliveSec = _config.HeartbeatCheckIntervalSeconds;
                struServerInfo.dwTimeOutCount = 3;
                //struServerInfo.dwAlarmKeepAliveSec = _config.HeartbeatCheckIntervalSeconds;
                //struServerInfo.dwAlarmTimeOutCount = 3;
                Marshal.StructureToPtr(struServerInfo, pInBuffer, false);

                _deviceLogger.LogDeviceInfo(deviceId, "设置心跳参数 - KeepAliveSec: {KeepAlive}, TimeOutCount: {TimeOut}",
                struServerInfo.dwKeepAliveSec, struServerInfo.dwTimeOutCount);
                // 异步注册设备，避免阻塞回调
                Task.Run(async () =>
                {
                    try
                    {
                        var (Success, Message, DeviceId) = await _deviceManager.RegisterDevice(lUserID, struDevInfo);
                        if (Success)
                        {
                            _deviceLogger.LogDeviceInfo(deviceId, "设备注册成功");
                        }
                        else
                        {
                            _deviceLogger.LogDeviceError(deviceId, null, "设备注册失败: {Message}", Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _deviceLogger.LogDeviceError(deviceId, ex, "异步设备注册异常");
                    }

                    return Task.CompletedTask;
                });
                return true;
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理设备上线异常");
                return false;
            }
        }

        /// <summary>
        /// 处理设备心跳
        /// </summary>
        private async Task<bool> HandleDeviceHeartbeat(int lUserID, string deviceId)
        {
            try
            {
                _deviceLogger.LogDeviceInfo(deviceId, "收到设备心跳: UserID: {UserID}", lUserID);

                bool result = await _deviceManager.UpdateDeviceHeartbeat(deviceId, lUserID);
                if (!result)
                {
                    _deviceLogger.LogDeviceWarning(deviceId, "更新设备心跳失败");
                }

                return true;
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理设备心跳异常");
                return false;
            }
        }

        /// <summary>
        /// 处理设备下线
        /// </summary>
        private bool HandleDeviceOffline(int lUserID, string deviceId)
        {
            try
            {
                _deviceLogger.LogDeviceInfo(deviceId, "设备下线: UserID: {UserID}", lUserID);
                // 异步断开设备连接
                _ = Task.Run(() =>
                {
                    try
                    {
                        _deviceManager.RemoveDevice(deviceId);
                    }
                    catch (Exception ex)
                    {
                        _deviceLogger.LogDeviceError(deviceId, ex, "异步断开设备连接异常");
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理设备下线异常");
                return false;
            }
        }

        /// <summary>
        /// 处理设备认证
        /// </summary>
        private bool HandleDeviceAuth(string deviceId, IntPtr pInBuffer)
        {
            try
            {
                var ISUPKey = _config.ISUPKey;
                byte[] byTemp = Encoding.Default.GetBytes(ISUPKey);
                byte[] byISUPKey = new byte[32];
                byTemp.CopyTo(byISUPKey, 0);
                Marshal.Copy(byISUPKey, 0, pInBuffer, 32);
                _deviceLogger.LogDeviceInfo(deviceId, "设备认证完成");
                return true;
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理设备认证异常");
                return false;
            }
        }

        /// <summary>
        /// 处理DAS请求
        /// </summary>
        private bool HandleDasRequest(IntPtr pInBuffer)
        {
            try
            {
                var dasServerIP = _config.DasServerIP;
                var dasServerPort = _config.DasServerPort;
                string strInBuffer =
                        "{\n" +
                             "    \"Type\":\"DAS\",\n" +
                             "    \"DasInfo\": {\n" +
                             "        \"Address\":\"" + dasServerIP + "\",\n" +
                             "        \"Domain\":\"\",\n" +
                             "        \"ServerID\":\"\",\n" +
                             "        \"Port\":" + dasServerPort + ",\n" +
                             "        \"UdpPort\":" + dasServerPort + "\n" +
                             "    }\n" +
                             "}";
                byte[] byInBuffer = Encoding.Default.GetBytes(strInBuffer);
                Marshal.Copy(byInBuffer, 0, pInBuffer, byInBuffer.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理DAS请求异常");
                return false;
            }
        }

        /// <summary>
        /// 存储回调
        /// </summary>
        //public void FStorageCallback(int iUserID, ref OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        //{
        //    try
        //    {
        //        string deviceId = new string(pParam.szDevID).TrimEnd('\0');
        //        _deviceLogger.LogDeviceDebug(deviceId, "存储回调: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);

        //        switch (pParam.dwType)
        //        {
        //            case ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY:
        //                HandleStorageUploadQuery(iUserID, ref pParam);
        //                break;

        //            case ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT:
        //                HandleStorageUploadReport(iUserID, ref pParam);
        //                break;

        //            default:
        //                _deviceLogger.LogDeviceWarning(deviceId, "未处理的存储回调类型: {Type}", pParam.dwType);
        //                break;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "存储回调处理异常: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
        //    }
        //}

        /// <summary>
        /// 订阅消息回调
        /// </summary>
        //public bool FSubscribeMsgCallback(int iUserID, ref OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        //{
        //    string deviceID = Encoding.UTF8.GetString(pParam.szDevID).TrimEnd('\0');
        //    try
        //    {
        //        _deviceLogger.LogDeviceDebug(deviceID, "订阅消息回调: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
        //        string szDomain = Encoding.UTF8.GetString(pParam.szDomain).TrimEnd('\0');
        //        string szIdentifier = Encoding.UTF8.GetString(pParam.szIdentifier).TrimEnd('\0');
        //        switch (pParam.dwType)
        //        {
        //            //case ENUM_OTAP_CMS_ATTRIBUTE_REPORT_MODEL:
        //            //    HandleAttributeReport(deviceID, szDomain, szIdentifier, pParam);
        //            //    break;

        //            //case ENUM_OTAP_CMS_SERVICE_QUERY_MODEL:
        //            //    HandleServiceQuery(deviceID, szDomain, szIdentifier, pParam);
        //            //    break;

        //            //case ENUM_OTAP_CMS_EVENT_REPORT_MODEL:
        //            //    HandleEventReport(deviceID, szDomain, szIdentifier, pParam);
        //            //    break;

        //            default:
        //                _deviceLogger.LogDeviceWarning(deviceID, "未处理的订阅消息类型: {Type}, Domain: {Domain}, Identifier: {Identifier}",
        //                    pParam.dwType, szDomain, szIdentifier);
        //                break;
        //        }

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        _deviceLogger.LogDeviceError(deviceID, ex, "订阅消息回调处理异常: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
        //        return false;
        //    }
        //}

        public bool AlarmMsgCallBack(int iHandle, IntPtr pAlarmMsg, IntPtr pUser)
        {
            NET_EHOME_ALARM_MSG struAlarmMsg = new();
            struAlarmMsg.Init();
            struAlarmMsg = (NET_EHOME_ALARM_MSG)Marshal.PtrToStructure(pAlarmMsg, typeof(NET_EHOME_ALARM_MSG));
            _logger.LogInformation($"AlarmType:{struAlarmMsg.dwAlarmType}, dwAlarmInfoLen:{struAlarmMsg.dwAlarmInfoLen}, dwXmlBufLen:{struAlarmMsg.dwXmlBufLen},pUser:{pUser}");
            if (struAlarmMsg.dwXmlBufLen != 0 & struAlarmMsg.pXmlBuf != IntPtr.Zero)
            {
                byte[] byXmlData = new byte[struAlarmMsg.dwXmlBufLen];
                Marshal.Copy(struAlarmMsg.pXmlBuf, byXmlData, 0, (int)struAlarmMsg.dwXmlBufLen);
            }
            ProcessAlarmData(struAlarmMsg.dwAlarmType, struAlarmMsg.pAlarmInfo, struAlarmMsg.dwAlarmInfoLen, struAlarmMsg.pXmlBuf, struAlarmMsg.dwXmlBufLen);
            return true;
        }

        public void ProcessAlarmData(uint dwAlarmType, IntPtr pAlarmInfo, uint dwAlarmInfoLen, IntPtr pXmlBuf, uint dwXmlBufLen)
        {
            switch (dwAlarmType)
            {
                case EHOME_ISAPI_ALARM://上报事件
                    NET_EHOME_ALARM_ISAPI_INFO struISAPIAlarm = new();
                    if (pAlarmInfo != IntPtr.Zero)
                        struISAPIAlarm = (NET_EHOME_ALARM_ISAPI_INFO)Marshal.PtrToStructure(pAlarmInfo, typeof(NET_EHOME_ALARM_ISAPI_INFO));

                    if (struISAPIAlarm.pAlarmData != IntPtr.Zero & struISAPIAlarm.dwAlarmDataLen != 0)
                    {

                        byte[] alarmData = new byte[struISAPIAlarm.dwAlarmDataLen];
                        Marshal.Copy(struISAPIAlarm.pAlarmData, alarmData, 0, (int)struISAPIAlarm.dwAlarmDataLen);
                        string strAlarmData = Encoding.UTF8.GetString(alarmData);
                        _logger.LogInformation("strAlarmData:" + strAlarmData);
                        HandleEventReport(strAlarmData);
                    }
                    break;
            }
        }


        ///// <summary>
        ///// 处理属性上报
        ///// </summary>
        //private void HandleAttributeReport(string deviceID, string domain, string identifier, OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam)
        //{
        //    try
        //    {
        //        if (pParam.pOutBuf == IntPtr.Zero || pParam.dwOutBufSize == 0) return;

        //        byte[] byOutbuffer = new byte[pParam.dwOutBufSize];
        //        Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
        //        string strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');

        //        _deviceLogger.LogDeviceInfo(deviceID, "属性上报 - Domain: {Domain}, Identifier: {Identifier}, Data: {Data}",
        //            domain, identifier, strOutbuffer.Truncate(200));
        //    }
        //    catch (Exception ex)
        //    {
        //        _deviceLogger.LogDeviceError(deviceID, ex, "处理属性上报异常");
        //    }
        //}

        ///// <summary>
        ///// 处理服务查询
        ///// </summary>
        //private void HandleServiceQuery(string deviceID, string domain, string identifier, OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam)
        //{
        //    try
        //    {
        //        if (pParam.pOutBuf == IntPtr.Zero || pParam.dwOutBufSize == 0) return;

        //        byte[] byOutbuffer = new byte[pParam.dwOutBufSize];
        //        Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
        //        string strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');

        //        _deviceLogger.LogDeviceInfo(deviceID, "服务查询 - Domain: {Domain}, Identifier: {Identifier}, Data: {Data}",
        //            domain, identifier, strOutbuffer.Truncate(200));
        //    }
        //    catch (Exception ex)
        //    {
        //        _deviceLogger.LogDeviceError(deviceID, ex, "处理服务查询异常");
        //    }
        //}

        /// <summary>
        /// 处理事件报告
        /// </summary>
        private async Task HandleEventReport(string strAlarmInfo)
        {
            try
            {
                AlarmMsgInfo alarmMsgInfo = JsonSerializer.Deserialize<AlarmMsgInfo>(strAlarmInfo);
                DeviceEvent deviceEvent = new()
                {
                    EventType = "EventReport",
                    DeviceId = alarmMsgInfo.DeviceID,
                    Payload = strAlarmInfo
                };
                await _deviceManager.PublishDeviceEvent(deviceEvent);

                _deviceLogger.LogDeviceInfo(alarmMsgInfo.DeviceID, "事件报告 -AlarmInfo: {AlarmInfo}", strAlarmInfo);
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(strAlarmInfo, ex, "处理事件报告异常");
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void StopListen()
        {
            try
            {
                if (CMSServiceHelpers.CmsListenHandle > 0)
                {
                    if (!NET_ECMS_StopListen(CMSServiceHelpers.CmsListenHandle))
                    {
                        var errorCode = NET_ECMS_GetLastError();
                        _logger.LogError("NET_ECMS_StopListen failed, error: {ErrorCode}", errorCode);
                    }
                    else
                    {
                        _logger.LogInformation("NET_ECMS_StopListen 成功, Handle: {Handle}", CMSServiceHelpers.CmsListenHandle);
                        CMSServiceHelpers.CmsListenHandle = -1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止监听异常");
            }
        }

        /// <summary>
        /// 根据当前平台获取库文件路径
        /// </summary>
        /// <param name="libraryName">库文件基础名称（不包含扩展名）</param>
        /// <returns>完整的库文件路径</returns>
        private static string GetPlatformLibraryPath(string libraryName)
        {
            string basePath = CMSServiceHelpers.sCurPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows平台库文件映射
                var windowsLibraries = new Dictionary<string, string>
                {
                    { "libeay32", "libeay32.dll" },
                    { "ssleay32", "ssleay32.dll" },
                };

                if (windowsLibraries.TryGetValue(libraryName, out string? fileName))
                {
                    return Path.Combine(basePath, "Libs", "Windows", fileName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux平台库文件映射
                var linuxLibraries = new Dictionary<string, string>
                {
                    { "libeay32", "libcrypto.so" },
                    { "ssleay32", "libssl.so" },
                };

                if (linuxLibraries.TryGetValue(libraryName, out string? fileName))
                {
                    return Path.Combine(basePath, "Libs", "Linux", fileName);
                }
            }

            // 如果找不到对应的库文件，返回默认路径
            Console.WriteLine($"Warning: 未找到平台 {RuntimeInformation.OSDescription} 的库文件 {libraryName}");
            return Path.Combine(basePath, libraryName);
        }

        /// <summary>
        /// 设置依赖库路径
        /// </summary>
        private void SetupDependencyLibraries()
        {
            var libraries = new[]
            {
                ("libeay32", NET_EHOME_CMS_INIT_CFG_LIBEAY_PATH),
                ("ssleay32", NET_EHOME_CMS_INIT_CFG_SSLEAY_PATH),
            };
            foreach (var (libName, configType) in libraries)
            {
                try
                {
                    string libPath = GetPlatformLibraryPath(libName);

                    // 检查文件是否存在
                    if (!File.Exists(libPath))
                    {
                        _logger.LogWarning("库文件不存在: {LibPath}", libPath);
                        continue;
                    }
                    if (!NET_ECMS_SetSDKInitCfg(configType, Marshal.StringToHGlobalAnsi(libPath)))
                    {
                        var errorCode = NET_ECMS_GetLastError();
                        _logger.LogError("中心服务设置库路径失败 {LibName}: {LibPath}, 错误码: {ErrorCode}", libName, libPath, errorCode);
                    }
                    if (!NET_EALARM_SetSDKInitCfg(configType, Marshal.StringToHGlobalAnsi(libPath)))
                    {
                        var errorCode = NET_ECMS_GetLastError();
                        _logger.LogError("监听服务设置库路径失败 {LibName}: {LibPath}, 错误码: {ErrorCode}", libName, libPath, errorCode);
                    }
                    else
                    {
                        _logger.LogDebug("设置库路径成功 {LibName}: {LibPath}", libName, libPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "设置库路径异常 {LibName}", libName);
                }
            }
        }
    }
}