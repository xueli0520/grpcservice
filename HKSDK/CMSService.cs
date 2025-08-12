using GrpcService.Common;
using GrpcService.Models;
using GrpcService.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using static GrpcService.HKSDK.HCOTAPCMS;

namespace GrpcService.HKSDK
{
    public class CMSService : IHostedService, IDisposable
    {
        private readonly DeviceManager _deviceManager;
        private readonly ILogger<CMSService> _logger;
        private readonly IDeviceLoggerService _deviceLogger;
        private readonly HikDeviceConfiguration _config;
        private readonly LibraryPathsConfiguration _libraryConfig;
        private readonly object _initLock = new();
        private readonly object _disposeLock = new();
        private bool _isInitialized = false;
        private bool _disposed = false;

        // 常量提取
        private const int MaxIpLength = 127;
        private const int MaxBucketLength = 63;
        private const int MaxRegionLength = 31;
        private const string DefaultTopicFilter = "#model#\r\n";
        private const string SdkLogDir = "SdkLog";

        public CMSService(
            ILogger<CMSService> logger,
            DeviceManager deviceManager,
            IDeviceLoggerService deviceLogger,
            IOptions<HikDeviceConfiguration> config,
            IOptions<LibraryPathsConfiguration> libraryConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _deviceLogger = deviceLogger ?? throw new ArgumentNullException(nameof(deviceLogger));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _libraryConfig = libraryConfig?.Value ?? throw new ArgumentNullException(nameof(libraryConfig));

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
            if (string.IsNullOrWhiteSpace(_config.DasServerIP))
                throw new ArgumentException("DasServerIP 配置不能为空");
            if (_config.DasServerPort <= 0)
                throw new ArgumentException("DasServerPort 配置无效");
            if (string.IsNullOrWhiteSpace(_config.PicServerIP))
                throw new ArgumentException("PicServerIP 配置不能为空");
            if (_config.PicServerPort <= 0)
                throw new ArgumentException("PicServerPort 配置无效");
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
                    _logger.LogInformation("开始初始化CMS服务...");
                    _logger.LogDebug("当前平台: {Platform}", GetPlatformInfo());
                    SetupDependencyLibraries();
                    // 初始化SDK
                    if (!OTAP_CMS_Init())
                    {
                        var errorCode = OTAP_CMS_GetLastError();
                        var errorMessage = $"OTAP_CMS_Init failed, error: {errorCode}";
                        _logger.LogError(errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }
                    //配置设备心跳 
                    IntPtr pBuffer = IntPtr.Zero;
                    pBuffer = Marshal.AllocHGlobal(sizeof(bool));
                    Marshal.WriteByte(pBuffer, 1); // TRUE = 1
                    if (!OTAP_CMS_SetSDKLocalCfg(ENUM_OTAP_CMS_DEV_DAS_PINGREQ_CALLBACK, pBuffer))
                    {
                        var errorCode = OTAP_CMS_GetLastError();
                        var errorMessage = $"OTAP_CMS_Init failed, error: {errorCode}";
                        _logger.LogError(errorMessage);
                        Console.WriteLine(errorMessage);
                    }
                    _logger.LogInformation("心跳设置 成功");

                    _logger.LogInformation("OTAP_CMS_Init 初始化成功!");

                    // 设置日志
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SdkLogDir);
                    CommonMethod.EnsureDirectoryExists(logPath);
                    OTAP_CMS_SetLogToFile(3, logPath, false);

                    // 订阅消息
                    SubscribeMessages();

                    // 订阅存储消息
                    SubscribeStorageMessages();

                    // 启动监听
                    StartListen();

                    _isInitialized = true;
                    _logger.LogInformation("CMS服务初始化完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CMS服务初始化失败");
                    throw;
                }
            }
        }


        /// <summary>
        /// 订阅消息
        /// </summary>
        private void SubscribeMessages()
        {
            IntPtr ptrSubscribeMsgParam = IntPtr.Zero;
            try
            {
                CMSServiceHelpers.OTAP_SubscribeMsgCallback_Func ??= new OTAP_CMS_SubscribeMsgCallback(FSubscribeMsgCallback);
                CMSServiceHelpers.subscribeMsgParam.fnCB = CMSServiceHelpers.OTAP_SubscribeMsgCallback_Func;

                int size = Marshal.SizeOf(CMSServiceHelpers.subscribeMsgParam);
                ptrSubscribeMsgParam = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(CMSServiceHelpers.subscribeMsgParam, ptrSubscribeMsgParam, false);

                if (!OTAP_CMS_SubscribeMsg(OTAP_CMS_SUBSCRIBE_MSG_ENUM.ENUM_OTAP_CMS_SET_CALLBACK_FUN, ptrSubscribeMsgParam))
                {
                    var errorCode = OTAP_CMS_GetLastError();
                    _logger.LogError("OTAP_CMS_SubscribeMsg failed, error: {ErrorCode}", errorCode);
                }
                else
                {
                    _logger.LogInformation("OTAP_CMS_SubscribeMsg succ");
                    CMSServiceHelpers.CmsListenHandle = -1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅消息异常");
            }
            finally
            {
                if (ptrSubscribeMsgParam != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptrSubscribeMsgParam);
                OTAP_CMS_SUBSCRIBEMSG_TOPIC_FILTER_PARAM struTopicFilter = new();
                struTopicFilter.Init();

                string szTopicFilter = "#\r\n";//订阅的主题
                struTopicFilter.dwTopicFilterLen = (uint)szTopicFilter.Length;
                struTopicFilter.pTopicFilter = Marshal.StringToHGlobalAnsi(szTopicFilter);

                int size = Marshal.SizeOf(struTopicFilter);
                IntPtr ptrStruTopicFilter = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(struTopicFilter, ptrStruTopicFilter, false);

                if (!OTAP_CMS_SubscribeMsg(OTAP_CMS_SUBSCRIBE_MSG_ENUM.ENUM_OTAP_CMS_SET_TOPIC_FILTER, ptrStruTopicFilter))
                {
                    Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_TOPIC_FILTER failed, error:" + OTAP_CMS_GetLastError());
                }
                else
                {
                    Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_TOPIC_FILTER succ!");
                }
            }
        }

        /// <summary>
        /// 获取服务状态
        /// </summary>
        public Dictionary<string, object> GetServiceStatus()
        {
            return new Dictionary<string, object>
            {
                ["initialized"] = _isInitialized,
                ["listen_handle"] = CMSServiceHelpers.CmsListenHandle,
                ["platform"] = GetPlatformInfo(),
                ["configuration"] = new Dictionary<string, object>
                {
                    ["cms_server"] = $"{_config.CmsServerIP}:{_config.CmsServerPort}",
                    ["das_server"] = $"{_config.DasServerIP}:{_config.DasServerPort}",
                    ["pic_server"] = $"{_config.PicServerIP}:{_config.PicServerPort}",
                    ["heartbeat_timeout"] = _config.HeartbeatTimeoutSeconds,
                    ["max_concurrent_operations"] = _config.MaxConcurrentOperations
                },
                ["device_statistics"] = _deviceManager.GetDeviceStatistics()
            };
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
                            if (!OTAP_CMS_Fini())
                            {
                                var errorCode = OTAP_CMS_GetLastError();
                                _logger.LogError("OTAP_CMS_Fini failed, error: {ErrorCode}", errorCode);
                            }
                            else
                            {
                                _logger.LogInformation("OTAP_CMS_Fini 成功");
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

                CMSServiceHelpers.OTAP_REGISTER_Func ??= new OTAP_CMS_RegisterCallback(FRegisterCallBack);
                CMSServiceHelpers.cmsListenParam.fnCB = CMSServiceHelpers.OTAP_REGISTER_Func;
                CMSServiceHelpers.cmsListenParam.pUserData = IntPtr.Zero;

                CMSServiceHelpers.CmsListenHandle = OTAP_CMS_StartListen(ref CMSServiceHelpers.cmsListenParam);
                if (CMSServiceHelpers.CmsListenHandle < 0)
                {
                    var errorCode = OTAP_CMS_GetLastError();
                    var errorMessage = $"OTAP_CMS_StartListen failed, error: {errorCode}";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogInformation("OTAP_CMS_StartListen 启动成功, IP: {IP}, Port: {Port}, Handle: {Handle}",
                    cmsServerIP, _config.CmsServerPort, CMSServiceHelpers.CmsListenHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监听失败");
                throw;
            }
        }

        /// <summary>
        /// 订阅存储消息
        /// </summary>
        private void SubscribeStorageMessages()
        {
            try
            {
                CMSServiceHelpers.OTAP_CMS_StorageCallback_Func ??= new OTAP_CMS_StorageCallback(FStorageCallback);
                CMSServiceHelpers.struStorageCBParam.fnCB = CMSServiceHelpers.OTAP_CMS_StorageCallback_Func;

                if (!OTAP_CMS_SubscribeStorageMsg(ref CMSServiceHelpers.struStorageCBParam))
                {
                    var errorCode = OTAP_CMS_GetLastError();
                    _logger.LogError("OTAP_CMS_SubscribeStorageMsg failed, error: {ErrorCode}", errorCode);
                }
                else
                {
                    _logger.LogInformation("OTAP_CMS_SubscribeStorageMsg succ!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅存储消息失败");
            }
        }

        /// <summary>
        /// 设备注册回调
        /// </summary>
        public unsafe int FRegisterCallBack(int lUserID, uint dwDataType, IntPtr pOutBuffer, uint dwOutLen, IntPtr pInBuffer, uint dwInLen, IntPtr pUserData)
        {
            try
            {
                _logger.LogDebug("FRegisterCallBack, dwDataType: {DataType}, lUserID: {UserID}", dwDataType, lUserID);

                OTAP_CMS_DEV_REG_INFO struDevInfo = new();
                struDevInfo.Init();
                struDevInfo.struDevAddr.Init();
                struDevInfo.struRegAddr.Init();
                if (pOutBuffer != IntPtr.Zero)
                {
                    struDevInfo = (OTAP_CMS_DEV_REG_INFO)Marshal.PtrToStructure(pOutBuffer, typeof(OTAP_CMS_DEV_REG_INFO))!;
                }

                string strDeviceID = Encoding.Default.GetString(struDevInfo.byDeviceID).TrimEnd('\0');

                switch (dwDataType)
                {
                    case ENUM_OTAP_CMS_DEV_ON:
                    case ENUM_OTAP_CMS_ADDRESS_CHANGED:
                    case ENUM_OTAP_CMS_DEV_DAS_REREGISTER:
                        HandleDeviceOnline(lUserID, strDeviceID, struDevInfo, pInBuffer, dwInLen);
                        break;
                    case ENUM_OTAP_CMS_DEV_DAS_PINGREQ:
                        HandleDeviceHeartbeat(lUserID, strDeviceID);
                        break;
                    case ENUM_OTAP_CMS_DEV_OFF:
                    case ENUM_OTAP_CMS_DEV_SESSIONKEY_ERROR:
                    case ENUM_OTAP_CMS_DEV_DAS_OTAPKEY_ERROR:
                        HandleDeviceOffline(lUserID, strDeviceID);
                        break;
                    case ENUM_OTAP_CMS_DEV_AUTH:
                        HandleDeviceAuth(strDeviceID, pInBuffer);
                        break;
                    case ENUM_OTAP_CMS_DAS_REQ:
                        HandleDasRequest(pInBuffer);
                        break;
                    case ENUM_OTAP_CMS_DEV_SESSIONKEY:
                        break;

                    default:
                        _logger.LogWarning("未处理的设备事件类型: {DataType}, DeviceID: {DeviceID}", dwDataType, strDeviceID);
                        break;
                }

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备注册回调处理异常, UserID: {UserID}, DataType: {DataType}", lUserID, dwDataType);
                return 0;
            }
        }

        /// <summary>
        /// 处理设备上线
        /// </summary>
        private bool HandleDeviceOnline(int lUserID, string deviceId, OTAP_CMS_DEV_REG_INFO struDevInfo, IntPtr pInBuffer, uint dwInLen)
        {
            try
            {
                _deviceLogger.LogDeviceInfo(deviceId, "设备上线: UserID: {UserID}", lUserID);
                // 设置服务器心跳参数
                var struServerInfo = Marshal.PtrToStructure<OTAP_CMS_SERVER_INFO>(pInBuffer)!;
                // CMS 心跳配置
                struServerInfo.dwKeepAliveSec = (uint)_config.HeartbeatCheckIntervalSeconds;
                struServerInfo.dwTimeOutCount = 3;
                Marshal.StructureToPtr(struServerInfo, pInBuffer, false);

                _deviceLogger.LogDeviceInfo(deviceId, "设置心跳参数 - KeepAliveSec: {KeepAlive}, TimeOutCount: {TimeOut}",
                    struServerInfo.dwKeepAliveSec, struServerInfo.dwTimeOutCount);
                // 异步注册设备，避免阻塞回调
                Task.Run(async () =>
                {
                    try
                    {
                        var (Success, Message, DeviceId) = await _deviceManager.RegisterDeviceAsync(lUserID, struDevInfo);
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
        private bool HandleDeviceHeartbeat(int lUserID, string deviceId)
        {
            try
            {
                _deviceLogger.LogDeviceInfo(deviceId, "收到设备心跳: UserID: {UserID}", lUserID);

                bool result = _deviceManager.UpdateDeviceHeartbeat(deviceId, lUserID);
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
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _deviceManager.DisconnectDeviceAsync(deviceId, lUserID);
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
                var otapKey = _config.OTAPKey;
                byte[] byOTAPKey = new byte[32];
                byte[] byTemp = Encoding.Default.GetBytes(otapKey);
                Array.Copy(byTemp, byOTAPKey, Math.Min(byTemp.Length, 32));

                Marshal.Copy(byOTAPKey, 0, pInBuffer, 32);

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
                OTAP_CMS_DAS_INFO struCmsDasInfo = new();
                struCmsDasInfo.Init();
                struCmsDasInfo.struDevAddr.Init();
                var dasServerIP = _config.DasServerIP;
                var dasServerPort = _config.DasServerPort;

                dasServerIP.CopyTo(0, struCmsDasInfo.struDevAddr.szIP, 0, dasServerIP.Length);
                struCmsDasInfo.struDevAddr.wPort = (short)dasServerPort;
                string strServerID = $"das_{dasServerIP}_{dasServerPort}";
                strServerID.CopyTo(0, struCmsDasInfo.byServerID, 0, strServerID.Length);

                if (pInBuffer != IntPtr.Zero)
                {
                    Marshal.StructureToPtr(struCmsDasInfo, pInBuffer, false);

                    _logger.LogInformation("处理DAS请求: ServerID: {ServerID}, IP: {IP}, Port: {Port}", strServerID, dasServerIP, dasServerPort);

                }
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
        public void FStorageCallback(int iUserID, ref OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        {
            try
            {
                string deviceId = new string(pParam.szDevID).TrimEnd('\0');
                _deviceLogger.LogDeviceDebug(deviceId, "存储回调: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);

                switch (pParam.dwType)
                {
                    case ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY:
                        HandleStorageUploadQuery(iUserID, ref pParam);
                        break;

                    case ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT:
                        HandleStorageUploadReport(iUserID, ref pParam);
                        break;

                    default:
                        _deviceLogger.LogDeviceWarning(deviceId, "未处理的存储回调类型: {Type}", pParam.dwType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "存储回调处理异常: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
            }
        }

        /// <summary>
        /// 处理存储上传查询
        /// </summary>
        private void HandleStorageUploadQuery(int iUserID, ref OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam)
        {
            var deviceId = new string(pParam.szDevID).TrimEnd('\0');
            try
            {
                if (pParam.pOutBuf == IntPtr.Zero) return;

                var struUpload = (OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM)Marshal.PtrToStructure(pParam.pOutBuf, typeof(OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM))!;

                string childId = new string(pParam.szChildID).TrimEnd('\0');
                string localIndex = new string(pParam.szLocalIndex).TrimEnd('\0');
                string resourceType = new string(pParam.szResourceType).TrimEnd('\0');

                _deviceLogger.LogDeviceInfo(deviceId, "存储上传查询 - ChildID: {ChildID}, LocalIndex: {LocalIndex}, ResourceType: {ResourceType}, Sequence: {Sequence}",
                    childId, localIndex, resourceType, pParam.dwSequence);

                // 构建上传参数
                var struUploadObjInputParam = BuildUploadInputParam();

                // 响应上传查询
                var struStorageResponseMsg = new OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM();
                struStorageResponseMsg.Init();
                struStorageResponseMsg.szChildID = pParam.szChildID;
                struStorageResponseMsg.szLocalIndex = pParam.szLocalIndex;
                struStorageResponseMsg.szResourceType = pParam.szResourceType;
                struStorageResponseMsg.dwSequence = pParam.dwSequence;
                struStorageResponseMsg.dwType = ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY;
                IntPtr pInBuf = Marshal.AllocHGlobal(Marshal.SizeOf(struUploadObjInputParam));
                struStorageResponseMsg.pInBuf = pInBuf;
                struStorageResponseMsg.dwInBufSize = (uint)Marshal.SizeOf(struUploadObjInputParam);

                try
                {
                    Marshal.StructureToPtr(struUploadObjInputParam, pInBuf, false);

                    if (!OTAP_CMS_ResponseStorageMsg(iUserID, ref struStorageResponseMsg))
                    {
                        var errorCode = OTAP_CMS_GetLastError();
                        _deviceLogger.LogDeviceError(deviceId, null, "OTAP_CMS_ResponseStorageMsg UPLOAD_QUERY_REPLY failed, error: {ErrorCode}", errorCode);
                    }
                    else
                    {
                        _deviceLogger.LogDeviceInfo(deviceId, "OTAP_CMS_ResponseStorageMsg UPLOAD_QUERY_REPLY succ!");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pInBuf);
                }
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理存储上传查询异常");
            }
        }

        /// <summary>
        /// 处理存储上传报告
        /// </summary>
        private void HandleStorageUploadReport(int iUserID, ref OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam)
        {
            var deviceId = new string(pParam.szDevID).TrimEnd('\0');
            try
            {
                if (pParam.pOutBuf == IntPtr.Zero) return;

                var struReport = (OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM)Marshal.PtrToStructure(pParam.pOutBuf, typeof(OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM))!;

                string childId = new string(pParam.szChildID).TrimEnd('\0');
                string storageId = new string(struReport.szStorageId).TrimEnd('\0');
                string bucket = new string(struReport.szBucket).TrimEnd('\0');

                _deviceLogger.LogDeviceInfo(deviceId, "存储上传报告 - ChildID: {ChildID}, StorageID: {StorageID}, Bucket: {Bucket}, Result: {Result}",
                    childId, storageId, bucket, struReport.dwResult);

                // 响应上传报告
                var struStorageResponseMsg = new OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM();
                struStorageResponseMsg.Init();
                struStorageResponseMsg.szChildID = pParam.szChildID;
                struStorageResponseMsg.szLocalIndex = pParam.szLocalIndex;
                struStorageResponseMsg.szResourceType = pParam.szResourceType;
                struStorageResponseMsg.dwSequence = pParam.dwSequence;
                struStorageResponseMsg.dwType = ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY;
                struStorageResponseMsg.pInBuf = IntPtr.Zero;
                struStorageResponseMsg.dwInBufSize = 0;

                if (!OTAP_CMS_ResponseStorageMsg(iUserID, ref struStorageResponseMsg))
                {
                    var errorCode = OTAP_CMS_GetLastError();
                    _deviceLogger.LogDeviceError(deviceId, null, "OTAP_CMS_ResponseStorageMsg UPLOAD_REPORT_REPLY failed, error: {ErrorCode}", errorCode);
                }
                else
                {
                    _deviceLogger.LogDeviceInfo(deviceId, "OTAP_CMS_ResponseStorageMsg UPLOAD_REPORT_REPLY succ!");
                }
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceId, ex, "处理存储上传报告异常");
            }
        }

        /// <summary>
        /// 构建上传输入参数
        /// </summary>
        private OTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM BuildUploadInputParam()
        {
            var struUploadObjInputParam = new OTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM();
            struUploadObjInputParam.Init();

            // SS存储服务器配置
            var picServerIP = _config.PicServerIP;
            var picServerPort = _config.PicServerPort;

            picServerIP.CopyTo(0, struUploadObjInputParam.struAddress.szIP, 0, Math.Min(picServerIP.Length, MaxIpLength));
            struUploadObjInputParam.struAddress.wPort = (short)picServerPort;

            // 生成存储ID和对象键
            Guid uuid = Guid.NewGuid();
            string szStorageId = uuid.ToString();
            szStorageId.CopyTo(0, struUploadObjInputParam.szStorageId, 0, Math.Min(szStorageId.Length, 127));

            string szObjectKey = uuid.ToString();
            szObjectKey.CopyTo(0, struUploadObjInputParam.szObjectKey, 0, Math.Min(szObjectKey.Length, 127));

            // 存储配置
            var storage = _config.Storage;
            storage.Bucket.CopyTo(0, struUploadObjInputParam.szBucketName, 0, Math.Min(storage.Bucket.Length, MaxBucketLength));
            storage.AccessKey.CopyTo(0, struUploadObjInputParam.szAccessKey, 0, Math.Min(storage.AccessKey.Length, 127));
            storage.SecretKey.CopyTo(0, struUploadObjInputParam.szSecretKey, 0, Math.Min(storage.SecretKey.Length, 127));
            storage.Region.CopyTo(0, struUploadObjInputParam.szRegion, 0, Math.Min(storage.Region.Length, MaxRegionLength));

            struUploadObjInputParam.bHttps = 0; // 使用HTTP
            struUploadObjInputParam.byEncrypt = 0; // 不加密

            _logger.LogDebug("生成存储参数 - StorageID: {StorageID}, ObjectKey: {ObjectKey}, Bucket: {Bucket}",
                szStorageId, szObjectKey, storage.Bucket);

            return struUploadObjInputParam;
        }

        /// <summary>
        /// 订阅消息回调
        /// </summary>
        public bool FSubscribeMsgCallback(int iUserID, ref OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        {
            string deviceID = Encoding.UTF8.GetString(pParam.szDevID).TrimEnd('\0');
            try
            {
                _deviceLogger.LogDeviceDebug(deviceID, "订阅消息回调: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
                string szDomain = Encoding.UTF8.GetString(pParam.szDomain).TrimEnd('\0');
                string szIdentifier = Encoding.UTF8.GetString(pParam.szIdentifier).TrimEnd('\0');
                switch (pParam.dwType)
                {
                    case ENUM_OTAP_CMS_ATTRIBUTE_REPORT_MODEL:
                        HandleAttributeReport(deviceID, szDomain, szIdentifier, pParam);
                        break;

                    case ENUM_OTAP_CMS_SERVICE_QUERY_MODEL:
                        HandleServiceQuery(deviceID, szDomain, szIdentifier, pParam);
                        break;

                    case ENUM_OTAP_CMS_EVENT_REPORT_MODEL:
                        HandleEventReport(deviceID, szDomain, szIdentifier, pParam);
                        break;

                    default:
                        _deviceLogger.LogDeviceWarning(deviceID, "未处理的订阅消息类型: {Type}, Domain: {Domain}, Identifier: {Identifier}",
                            pParam.dwType, szDomain, szIdentifier);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceID, ex, "订阅消息回调处理异常: UserID: {UserID}, Type: {Type}", iUserID, pParam.dwType);
                return false;
            }
        }

        /// <summary>
        /// 处理属性上报
        /// </summary>
        private void HandleAttributeReport(string deviceID, string domain, string identifier, OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam)
        {
            try
            {
                if (pParam.pOutBuf == IntPtr.Zero || pParam.dwOutBufSize == 0) return;

                byte[] byOutbuffer = new byte[pParam.dwOutBufSize];
                Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
                string strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');

                _deviceLogger.LogDeviceInfo(deviceID, "属性上报 - Domain: {Domain}, Identifier: {Identifier}, Data: {Data}",
                    domain, identifier, strOutbuffer.Truncate(200));
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceID, ex, "处理属性上报异常");
            }
        }

        /// <summary>
        /// 处理服务查询
        /// </summary>
        private void HandleServiceQuery(string deviceID, string domain, string identifier, OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam)
        {
            try
            {
                if (pParam.pOutBuf == IntPtr.Zero || pParam.dwOutBufSize == 0) return;

                byte[] byOutbuffer = new byte[pParam.dwOutBufSize];
                Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
                string strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');

                _deviceLogger.LogDeviceInfo(deviceID, "服务查询 - Domain: {Domain}, Identifier: {Identifier}, Data: {Data}",
                    domain, identifier, strOutbuffer.Truncate(200));
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceID, ex, "处理服务查询异常");
            }
        }

        /// <summary>
        /// 处理事件报告
        /// </summary>
        private void HandleEventReport(string deviceID, string domain, string identifier, OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam)
        {
            try
            {
                if (pParam.pOutBuf == IntPtr.Zero || pParam.dwOutBufSize == 0) return;

                var struAlarmMsg = (OTAP_AMS_ALARM_MSG)Marshal.PtrToStructure(pParam.pOutBuf, typeof(OTAP_AMS_ALARM_MSG))!;

                string strAlarmInfoBuf = "";
                if (struAlarmMsg.pAlarmInfoBuf != IntPtr.Zero && struAlarmMsg.dwAlarmInfoLen > 0)
                {
                    byte[] byAlarmInfoBuf = new byte[struAlarmMsg.dwAlarmInfoLen];
                    Marshal.Copy(struAlarmMsg.pAlarmInfoBuf, byAlarmInfoBuf, 0, (int)struAlarmMsg.dwAlarmInfoLen);
                    strAlarmInfoBuf = Encoding.UTF8.GetString(byAlarmInfoBuf).TrimEnd('\0');
                }

                _deviceLogger.LogDeviceInfo(struAlarmMsg.szDeviceID, "事件报告 - Domain: {Domain}, Identifier: {Identifier}, Topic: {Topic}, AlarmInfo: {AlarmInfo}",
                    domain, identifier, struAlarmMsg.szAlarmTopic, strAlarmInfoBuf.Truncate(200));
            }
            catch (Exception ex)
            {
                _deviceLogger.LogDeviceError(deviceID, ex, "处理事件报告异常");
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
                    if (!OTAP_CMS_StopListen(CMSServiceHelpers.CmsListenHandle))
                    {
                        var errorCode = OTAP_CMS_GetLastError();
                        _logger.LogError("OTAP_CMS_StopListen failed, error: {ErrorCode}", errorCode);
                    }
                    else
                    {
                        _logger.LogInformation("OTAP_CMS_StopListen 成功, Handle: {Handle}", CMSServiceHelpers.CmsListenHandle);
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
                    { "libcrypto", "libcrypto-3-x64.dll" },
                    { "libssl", "libssl-3-x64.dll" },
                    { "libiconv2", "libiconv2.dll" },
                    { "libz", "zlib1.dll" }
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
                    { "libcrypto", "libcrypto.so.3" },
                    { "libssl", "libssl.so.3" },
                    { "libiconv2", "libiconv2.so" },
                    { "libz", "libz.so" }
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
                ("libcrypto", ENUM_OTAP_CMS_INIT_CFG_LIBEAY_PATH),
                ("libssl", ENUM_OTAP_CMS_INIT_CFG_SSLEAY_PATH),
                ("libiconv2", ENUM_OTAP_CMS_INIT_CFG_LIBICONV_PATH),
                ("libz", ENUM_OTAP_CMS_INIT_CFG_ZLIB_PATH)
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

                    if (!OTAP_CMS_SetSDKInitCfg(configType, Marshal.StringToHGlobalAnsi(libPath)))
                    {
                        var errorCode = OTAP_CMS_GetLastError();
                        _logger.LogError("设置库路径失败 {LibName}: {LibPath}, 错误码: {ErrorCode}", libName, libPath, errorCode);
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