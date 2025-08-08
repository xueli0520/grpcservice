using Google.Protobuf;
using System.Runtime.InteropServices;

namespace GrpcService.HKSDK
{
    public class HCOTAPCMS
    {
        private const string WINDOWS_DLL = "HCOTAPCMS.dll";
        private const string LINUX_SO = "libHCOTAPCMS.so";
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// 获取当前平台信息
        /// </summary>
        public static string GetPlatformInfo()
        {
            var platform = IsWindows ? "Windows" : IsLinux ? "Linux" : "Unknown";
            var architecture = RuntimeInformation.ProcessArchitecture.ToString();
            return $"{platform} ({architecture})";
        }

        /// <summary>
        /// 获取所需的动态库文件列表
        /// </summary>
        public static string[] GetRequiredLibraries()
        {
            if (IsWindows)
            {
                return [WINDOWS_DLL];
            }
            else
            {
                return [LINUX_SO];
            }
        }

        #region windows SDK
        // CN: 初始化CMS组件。
        // EN: Initialize CMS component.
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_Init", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_Init_Windows();
        // CN: 反初始化CMS组件
        // EN: Deinitialize CMS component
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_Fini", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_Fini_Windows();

        // CN: 获取CMS组件错误码
        // EN: Get CMS component error code
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_GetLastError", CallingConvention = CallingConvention.StdCall)]
        public static extern uint OTAP_CMS_GetLastError_Windows();

        // CN: 获取CMS组件版本
        // EN: Get CMS component version
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_GetBuildVersion", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_GetBuildVersion_Windows();

        // CN: CMS启动监听
        // EN: CMS start listening
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StartListen", CallingConvention = CallingConvention.StdCall)]
        public static extern int OTAP_CMS_StartListen_Windows(ref OTAP_CMS_LISTEN_PARAM lpListenParam);

        // CN: CMS停止监听
        // EN: CMS stop listening
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StopListen", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_StopListen_Windows(int iListenHandle);

        // CN: CMS强制注销设备
        // EN: CMS force logout device
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_ForceLogout", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_ForceLogout_Windows();

        // CN: CMS获得公私钥
        // EN: CMS get private and public keys
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_GetPriPubKey", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_GetPriPubKey_Windows(int iCryptoType, ref OTAP_CMS_PRI_PUB_KEY pECDHKey);

        // CN: 获得SessionID
        // EN: Get SessionID
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_GetSevrSessionId", CallingConvention = CallingConvention.StdCall)]
        public static extern uint OTAP_CMS_GetSevrSessionId_Windows(nint pServSessionIDBuf, uint dwBufLen);

        // CN: 设置本地日志
        // EN: Set local log
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_SetLogToFile", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_SetLogToFile_Windows(int iLogLevel, string pLogDir, bool dwAutoDel);

        // CN: CMS通知设备开始实时取流
        // EN: CMS notify device to start live streaming
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StartLiveStreaming", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_StartLiveStreaming_Windows(int iUserID, int enumStreamingMode, nint pParamIn, nint pParamOut);

        // CN: CMS参数配置
        // EN: CMS parameter configuration
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_ConfigDev", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_ConfigDev_Windows(int iUserID, OTAP_CMS_CONFIG_DEV_ENUM enumMsg, ref OTAP_CMS_CONFIG_DEV_PARAM pParam);

        // CN: 开启语音对讲
        // EN: Start voice intercom
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StartVoice", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_StartVoice_Windows(int iUserID, ref OTAP_CMS_STARTVOICE_IN pVoiceIn, out OTAP_CMS_STARTVOICE_OUT pVoiceOut);

        // CN: 停止语音对讲
        // EN: Stop voice intercom
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StopVoice", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_StopVoice_Windows(int iUserID, ref OTAP_CMS_STOPVOICE_PARAM pStopParam);

        // CN: ISAPI透传
        // EN: ISAPI pass-through
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_ISAPIPassThrough", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_ISAPIPassThrough_Windows(int iUserID, ref OTAP_CMS_ISAPI_PT_PARAM pParam);

        // CN: CMS订阅消息
        // EN: CMS subscribe message
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_SubscribeMsg", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_SubscribeMsg_Windows(OTAP_CMS_SUBSCRIBE_MSG_ENUM enumSubscribeMsg, nint pParam);

        // CN: 开启OTAP协议设备报警上报
        // EN: Start OTAP protocol device alarm reporting
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_StartAlarm", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_StartAlarm_Windows(int iUserID, ref OTAP_CMS_STARTALARM_PARAM pParam);

        // CN: 配置简单存储订阅消息回调
        // EN: Configure simple storage subscription message callback
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_SubscribeStorageMsg", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_SubscribeStorageMsg_Windows(ref OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM pParam);

        // CN: 响应简单存储消息.
        // EN: Respond to simple storage message.
        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_ResponseStorageMsg", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_ResponseStorageMsg_Windows(int iUserID, ref OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM pParam);

        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_SetSDKInitCfg", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_SetSDKInitCfg_Windows(int enumType, nint lpInBuff);

        [DllImport(WINDOWS_DLL, EntryPoint = "OTAP_CMS_ResponseMsg", CallingConvention = CallingConvention.StdCall)]
        public static extern bool OTAP_CMS_ResponseMsg_Windows(int lUserID, int enumMsg, ref OTAP_CMS_RESPONSE_MSG_PARAM pParam);
        #endregion
        #region Linux SDK
        // CN: 初始化CMS组件。
        // EN: Initialize CMS component.
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_Init", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_Init_Linux();
        // CN: 反初始化CMS组件
        // EN: Deinitialize CMS component
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_Fini", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_Fini_Linux();

        // CN: 获取CMS组件错误码
        // EN: Get CMS component error code
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_GetLastError", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint OTAP_CMS_GetLastError_Linux();

        // CN: 获取CMS组件版本
        // EN: Get CMS component version
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_GetBuildVersion", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_GetBuildVersion_Linux();

        // CN: CMS启动监听
        // EN: CMS start listening
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StartListen", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OTAP_CMS_StartListen_Linux(ref OTAP_CMS_LISTEN_PARAM lpListenParam);

        // CN: CMS停止监听
        // EN: CMS stop listening
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StopListen", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_StopListen_Linux(int iListenHandle);

        // CN: CMS强制注销设备
        // EN: CMS force logout device
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_ForceLogout", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_ForceLogout_Linux();

        // CN: CMS获得公私钥
        // EN: CMS get private and public keys
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_GetPriPubKey", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_GetPriPubKey_Linux(int iCryptoType, ref OTAP_CMS_PRI_PUB_KEY pECDHKey);

        // CN: 获得SessionID
        // EN: Get SessionID
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_GetSevrSessionId", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint OTAP_CMS_GetSevrSessionId_Linux(nint pServSessionIDBuf, uint dwBufLen);

        // CN: 设置本地日志
        // EN: Set local log
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_SetLogToFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_SetLogToFile_Linux(int iLogLevel, string pLogDir, bool dwAutoDel);

        // CN: CMS通知设备开始实时取流
        // EN: CMS notify device to start live streaming
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StartLiveStreaming", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_StartLiveStreaming_Linux(int iUserID, int enumStreamingMode, nint pParamIn, nint pParamOut);

        // CN: CMS参数配置
        // EN: CMS parameter configuration
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_ConfigDev", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_ConfigDev_Linux(int iUserID, OTAP_CMS_CONFIG_DEV_ENUM enumMsg, ref OTAP_CMS_CONFIG_DEV_PARAM pParam);

        // CN: 开启语音对讲
        // EN: Start voice intercom
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StartVoice", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_StartVoice_Linux(int iUserID, ref OTAP_CMS_STARTVOICE_IN pVoiceIn, out OTAP_CMS_STARTVOICE_OUT pVoiceOut);

        // CN: 停止语音对讲
        // EN: Stop voice intercom
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StopVoice", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_StopVoice_Linux(int iUserID, ref OTAP_CMS_STOPVOICE_PARAM pStopParam);

        // CN: ISAPI透传
        // EN: ISAPI pass-through
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_ISAPIPassThrough", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_ISAPIPassThrough_Linux(int iUserID, ref OTAP_CMS_ISAPI_PT_PARAM pParam);

        // CN: CMS订阅消息
        // EN: CMS subscribe message
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_SubscribeMsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_SubscribeMsg_Linux(OTAP_CMS_SUBSCRIBE_MSG_ENUM enumSubscribeMsg, nint pParam);

        // CN: 开启OTAP协议设备报警上报
        // EN: Start OTAP protocol device alarm reporting
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_StartAlarm", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_StartAlarm_Linux(int iUserID, ref OTAP_CMS_STARTALARM_PARAM pParam);

        // CN: 配置简单存储订阅消息回调
        // EN: Configure simple storage subscription message callback
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_SubscribeStorageMsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_SubscribeStorageMsg_Linux(ref OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM pParam);

        // CN: 响应简单存储消息.
        // EN: Respond to simple storage message.
        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_ResponseStorageMsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_ResponseStorageMsg_Linux(int iUserID, ref OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM pParam);

        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_SetSDKInitCfg", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_SetSDKInitCfg_Linux(int enumType, nint lpInBuff);

        [DllImport(LINUX_SO, EntryPoint = "OTAP_CMS_ResponseMsg", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OTAP_CMS_ResponseMsg_Linux(int lUserID, int enumMsg, ref OTAP_CMS_RESPONSE_MSG_PARAM pParam);
        #endregion

        #region 跨平台
        // CN: 初始化CMS组件。
        // EN: Initialize CMS component.
        public static bool OTAP_CMS_Init()
        {
            return IsWindows ? OTAP_CMS_Init_Windows() : OTAP_CMS_Init_Linux();
        }

        // CN: 反初始化CMS组件
        // EN: Deinitialize CMS component
        public static bool OTAP_CMS_Fini()
        {
            return IsWindows ? OTAP_CMS_Fini_Windows() : OTAP_CMS_Fini_Linux();
        }

        // CN: 获取CMS组件错误码
        // EN: Get CMS component error code
        public static uint OTAP_CMS_GetLastError()
        {
            return IsWindows ? OTAP_CMS_GetLastError_Windows() : OTAP_CMS_GetLastError_Linux();
        }

        // CN: 获取CMS组件版本
        // EN: Get CMS component version
        public static bool OTAP_CMS_GetBuildVersion()
        {
            return IsWindows ? OTAP_CMS_GetBuildVersion_Windows() : OTAP_CMS_GetBuildVersion_Linux();
        }

        // CN: CMS启动监听
        // EN: CMS start listening
        public static int OTAP_CMS_StartListen(ref OTAP_CMS_LISTEN_PARAM lpListenParam)
        {
            return IsWindows ? OTAP_CMS_StartListen_Windows(ref lpListenParam) : OTAP_CMS_StartListen_Linux(ref lpListenParam);
        }

        // CN: CMS停止监听
        // EN: CMS stop listening
        public static bool OTAP_CMS_StopListen(int iListenHandle)
        {
            return IsWindows ? OTAP_CMS_StopListen_Windows(iListenHandle) : OTAP_CMS_StopListen_Linux(iListenHandle);
        }

        // CN: CMS强制注销设备
        // EN: CMS force logout device
        public static bool OTAP_CMS_ForceLogout()
        {
            return IsWindows ? OTAP_CMS_ForceLogout_Windows() : OTAP_CMS_ForceLogout_Linux();
        }

        // CN: CMS获得公私钥
        // EN: CMS get private and public keys
        public static bool OTAP_CMS_GetPriPubKey(int iCryptoType, ref OTAP_CMS_PRI_PUB_KEY pECDHKey)
        {
            return IsWindows ? OTAP_CMS_GetPriPubKey_Windows(iCryptoType, ref pECDHKey) : OTAP_CMS_GetPriPubKey_Linux(iCryptoType, ref pECDHKey);
        }

        // CN: 获得SessionID
        // EN: Get SessionID
        public static uint OTAP_CMS_GetSevrSessionId(nint pServSessionIDBuf, uint dwBufLen)
        {
            return IsWindows ? OTAP_CMS_GetSevrSessionId_Windows(pServSessionIDBuf, dwBufLen) : OTAP_CMS_GetSevrSessionId_Linux(pServSessionIDBuf, dwBufLen);
        }

        // CN: 设置本地日志
        // EN: Set local log
        public static bool OTAP_CMS_SetLogToFile(int iLogLevel, string pLogDir, bool dwAutoDel)
        {
            return IsWindows ? OTAP_CMS_SetLogToFile_Windows(iLogLevel, pLogDir, dwAutoDel) : OTAP_CMS_SetLogToFile_Linux(iLogLevel, pLogDir, dwAutoDel);
        }

        // CN: CMS通知设备开始实时取流
        // EN: CMS notify device to start live streaming
        public static bool OTAP_CMS_StartLiveStreaming(int iUserID, int enumStreamingMode, nint pParamIn, nint pParamOut)
        {
            return IsWindows ? OTAP_CMS_StartLiveStreaming_Windows(iUserID, enumStreamingMode, pParamIn, pParamOut) : OTAP_CMS_StartLiveStreaming_Linux(iUserID, enumStreamingMode, pParamIn, pParamOut);
        }

        // CN: CMS参数配置
        // EN: CMS parameter configuration
        public static bool OTAP_CMS_ConfigDev(int iUserID, OTAP_CMS_CONFIG_DEV_ENUM enumMsg, ref OTAP_CMS_CONFIG_DEV_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_ConfigDev_Windows(iUserID, enumMsg, ref pParam) : OTAP_CMS_ConfigDev_Linux(iUserID, enumMsg, ref pParam);
        }

        // CN: 开启语音对讲
        // EN: Start voice intercom
        public static bool OTAP_CMS_StartVoice(int iUserID, ref OTAP_CMS_STARTVOICE_IN pVoiceIn, out OTAP_CMS_STARTVOICE_OUT pVoiceOut)
        {
            return IsWindows ? OTAP_CMS_StartVoice_Windows(iUserID, ref pVoiceIn, out pVoiceOut) : OTAP_CMS_StartVoice_Linux(iUserID, ref pVoiceIn, out pVoiceOut);
        }

        // CN: 停止语音对讲
        // EN: Stop voice intercom
        public static bool OTAP_CMS_StopVoice(int iUserID, ref OTAP_CMS_STOPVOICE_PARAM pStopParam)
        {
            return IsWindows ? OTAP_CMS_StopVoice_Windows(iUserID, ref pStopParam) : OTAP_CMS_StopVoice_Linux(iUserID, ref pStopParam);
        }

        // CN: ISAPI透传
        // EN: ISAPI pass-through
        public static bool OTAP_CMS_ISAPIPassThrough(int iUserID, ref OTAP_CMS_ISAPI_PT_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_ISAPIPassThrough_Windows(iUserID, ref pParam) : OTAP_CMS_ISAPIPassThrough_Linux(iUserID, ref pParam);
        }

        // CN: CMS订阅消息
        // EN: CMS subscribe message
        public static bool OTAP_CMS_SubscribeMsg(OTAP_CMS_SUBSCRIBE_MSG_ENUM enumSubscribeMsg, nint pParam)
        {
            return IsWindows ? OTAP_CMS_SubscribeMsg_Windows(enumSubscribeMsg, pParam) : OTAP_CMS_SubscribeMsg_Linux(enumSubscribeMsg, pParam);
        }

        // CN: 开启OTAP协议设备报警上报
        // EN: Start OTAP protocol device alarm reporting
        public static bool OTAP_CMS_StartAlarm(int iUserID, ref OTAP_CMS_STARTALARM_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_StartAlarm_Windows(iUserID, ref pParam) : OTAP_CMS_StartAlarm_Linux(iUserID, ref pParam);
        }

        // CN: 配置简单存储订阅消息回调
        // EN: Configure simple storage subscription message callback
        public static bool OTAP_CMS_SubscribeStorageMsg(ref OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_SubscribeStorageMsg_Windows(ref pParam) : OTAP_CMS_SubscribeStorageMsg_Linux(ref pParam);
        }

        // CN: 响应简单存储消息.
        // EN: Respond to simple storage message.
        public static bool OTAP_CMS_ResponseStorageMsg(int iUserID, ref OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_ResponseStorageMsg_Windows(iUserID, ref pParam) : OTAP_CMS_ResponseStorageMsg_Linux(iUserID, ref pParam);
        }

        public static bool OTAP_CMS_SetSDKInitCfg(int enumType, nint lpInBuff)
        {
            return IsWindows ? OTAP_CMS_SetSDKInitCfg_Windows(enumType, lpInBuff) : OTAP_CMS_SetSDKInitCfg_Linux(enumType, lpInBuff);
        }

        public static bool OTAP_CMS_ResponseMsg(int lUserID, int enumMsg, ref OTAP_CMS_RESPONSE_MSG_PARAM pParam)
        {
            return IsWindows ? OTAP_CMS_ResponseMsg_Windows(lUserID, enumMsg, ref pParam) : OTAP_CMS_ResponseMsg_Linux(lUserID, enumMsg, ref pParam);
        }
        #endregion
        // CN: 设备上线回调. 相关结构体参见 ::tagOTAP_CMS_DEV_REG_INFO.
        // EN: Device online callback. See related structure ::tagOTAP_CMS_DEV_REG_INFO.
        public const int ENUM_OTAP_CMS_DEV_ON = 0;

        // CN: 设备下线回调
        // EN: Device offline callback
        public const int ENUM_OTAP_CMS_DEV_OFF = 1;

        // CN: 设备ping请求回调
        public const int ENUM_OTAP_CMS_DEV_DAS_PINGREQ_CALLBACK = 3;

        // CN: 设备地址发生变化(也表示设备已在线)
        public const int ENUM_OTAP_CMS_ADDRESS_CHANGED = 2;

        // CN: 设备认证回调. 相关结构体参见 ::tagOTAP_CMS_DEV_REG_INFO.
        // EN: Device authentication callback. See related structure ::tagOTAP_CMS_DEV_REG_INFO.
        public const int ENUM_OTAP_CMS_DEV_AUTH = 3;

        // CN: OTAP设备Sessionkey回调
        // EN: OTAP device Sessionkey callback
        public const int ENUM_OTAP_CMS_DEV_SESSIONKEY = 4;

        // CN: SessionKey请求(默认不回调), 负载均衡模式下需要调用 OTAP_CMS_SetSDKLocalCfg 接口开启
        // EN: SessionKey request (default no callback), needs to call OTAP_CMS_SetSDKLocalCfg interface to enable in load balancing mode
        public const int ENUM_OTAP_CMS_DEV_SESSIONKEY_REQ = 6;

        // CN: 设备超时时间内重注册(也表示设备已在线)
        public const int ENUM_OTAP_CMS_DEV_DAS_REREGISTER = 7;

        // CN: 注册心跳
        public const int ENUM_OTAP_CMS_DEV_DAS_PINGREQ = 8;

        // CN: OTAPKey错误
        public const int ENUM_OTAP_CMS_DEV_DAS_OTAPKEY_ERROR = 9;

        // CN: 设备注册SessionKey错误
        public const int ENUM_OTAP_CMS_DEV_SESSIONKEY_ERROR = 10;

        // CN: OTAP协议设备,AMS报警的SessionKey回调
        public const int ENUM_OTAP_CMS_DEV_ALARM_SESSIONKEY = 12;

        // CN: 设备请求DAS地址
        // EN: Device request DAS address
        public const int ENUM_OTAP_CMS_DAS_REQ = 13;

        public const int ENUM_OTAP_CMS_ATTRIBUTE_REPORT_MODEL = 0;  //< \~chinese 设备->平台. 原语属性上报
        public const int ENUM_OTAP_CMS_SERVICE_QUERY_MODEL = 1;  //< \~chinese 设备->平台. 原语上行操作
        public const int ENUM_OTAP_CMS_EVENT_REPORT_MODEL = 2;  //< \~chinese 设备->平台. CMS报警上报

        public const int ENUM_OTAP_CMS_ATTR_REPORT_REPLY = 0;   //< \~chinese 平台->设备,平台响应设备主动上报属性消息
        public const int ENUM_OTAP_CMS_SERVICE_QUERY_REPLY = 1; //< \~chinese 平台->设备,平台响应设备主动向平台查询消息

        //OpenSSL路径
        public const int ENUM_OTAP_CMS_INIT_CFG_LIBEAY_PATH = 0;   //< \~chinese 设置OpenSSL的libeay32.dll/libcrypto.so/libcrypto-1_1-x64.dll/libcrypto-1_1.dll/libcrypto-3.dll/libcrypto-3-x64.dll/libcrypto.so.1.1/libcrypto.so.3所在路径
        public const int ENUM_OTAP_CMS_INIT_CFG_SSLEAY_PATH = 1;   //< \~chinese 设置OpenSSL的ssleay32.dll/libssl.so/libssl.so.3/libssl-1_1-x64.dll/libssl-1_1.dll/libssl-3-x64.dll/libssl-3.dll/libssl.so.1.1所在路径

        public const int ENUM_OTAP_CMS_INIT_CFG_LIBICONV_PATH = 2; //< \~chinese 设置字符转码编码库libiconv2.dll/libiconv2.so所在路径
        public const int ENUM_OTAP_CMS_INIT_CFG_ZLIB_PATH = 3; //< \~chinese 设置压缩库zlib1.dll/libz.so所在路径

        public const int ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY = 1;//< \~chinese 设备->平台,订阅上传查询消息
        public const int ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT = 2;//< \~chinese 设备->平台,订阅上传结果上报消息
        public const int ENUM_OTAP_CMS_STORAGE_DOWNLOAD_QUERY = 3;//< \~chinese 设备->平台. [OTAP]订阅下载查询消息

        public const int ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY = 1;//< \~chinese 平台->设备,响应上传查询消息
        public const int ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY = 2;//< \~chinese 平台->设备,响应上传结果上报消息
        public const int ENUM_OTAP_CMS_STORAGE_DOWNLOAD_QUERY_REPLY = 3;//< \~chinese 平台->设备. [OTAP]响应下载查询消息

        // CN: AMS报警消息结构体
        // EN: AMS alarm message structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct OTAP_AMS_ALARM_MSG
        {
            // CN: 报警类型
            // EN: Alarm type
            public byte byAlarmType;

            // CN: 数据类型
            // EN: Data type
            public byte byDataType;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            // CN: 保留
            // EN: Reserved
            public byte[] byRes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            // CN: 设备ID
            // EN: Device ID
            public string szDeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            // CN: 报警主题
            // EN: Alarm topic
            public string szAlarmTopic;

            // CN: 报警信息长度
            // EN: Alarm information length
            public uint dwAlarmInfoLen;

            // CN: 报警信息缓冲区
            // EN: Alarm information buffer
            public nint pAlarmInfoBuf;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            // CN: 保留
            // EN: Reserved
            public byte[] byRes1;
        }

        // CN: 加密类型
        // EN: Encryption type
        public enum OTAP_CMS_CRYPTO_TYPE_ENUM
        {
            // CN: SECP 160 R1
            // EN: SECP 160 R1
            ENUM_OTAP_CMS_SECP_160_R1 = 0,
            // CN: SECP 224 R1
            // EN: SECP 224 R1
            ENUM_OTAP_CMS_SECP_224_R1 = 1,
            // CN: SECP 256 R1
            // EN: SECP 256 R1
            ENUM_OTAP_CMS_SECP_256_R1 = 2,
            // CN: SECP 384 R1
            // EN: SECP 384 R1
            ENUM_OTAP_CMS_SECP_384_R1 = 3,
            // CN: SECP 521 R1
            // EN: SECP 521 R1
            ENUM_OTAP_CMS_SECP_521_R1 = 4,
        };

        // CN: CMS取流方式
        // EN: CMS streaming mode
        public enum OTAP_CMS_STREAMING_MODE_ENUM
        {
            // CN: 一般取流方式
            // EN: General streaming mode
            ENUM_OTAP_CMS_STREAMING_MODE_NORMAL = 0,
            // CN: webRTC方式
            // EN: webRTC mode
            ENUM_OTAP_CMS_STREAMING_MODE_WEBRTC = 1
        };

        // CN: CMS设备配置类型
        // EN: CMS device configuration type
        public enum OTAP_CMS_CONFIG_DEV_ENUM
        {
            // CN: 属性获取
            // EN: Attribute acquisition
            OTAP_ENUM_OTAP_CMS_GET_MODEL_ATTR = 0,
            // CN: 属性设置
            // EN: Attribute setting
            OTAP_ENUM_OTAP_CMS_SET_MODEL_ATTR = 1,
            // CN: 下行操作
            // EN: Downlink operation
            OTAP_ENUM_OTAP_CMS_MODEL_SERVER_OPERATE = 2
        }

        // CN: CMS设备信息参数
        // EN: CMS device information parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_DEV_REG_INFO
        {
            [MarshalAs(UnmanagedType.Struct)]
            public OTAP_IPADDRESS struDevAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byDeviceID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] byDeviceName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byDeviceSerial;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byDeviceFullSerial;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] byFirmwareVersion;
            public uint dwDevType;
            public uint dwManufacture;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] bySessionKey;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byIV;
            [MarshalAs(UnmanagedType.Struct)]
            public OTAP_IPADDRESS struRegAddr;
            public byte byCompress;
            public byte byDecompress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes1;
            public uint dwDevTypeLen;
            public uint dwDevTypeDisplayLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes0;
            public nint pDevType;
            public nint pDevTypeDisplay;
            public nint pDevProtocols;
            public byte byDevProtocolVersion;
            public byte byProtocolVersion;
            public byte byDevProtocolsCount;
            public byte byWakeupMode;
            public byte byRegMode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 283)]
            public byte[] byRes;

            public void Init()
            {
                byDeviceID = new byte[256];
                byDeviceName = new byte[128];
                byDeviceSerial = new byte[256];
                byDeviceFullSerial = new byte[256];
                byFirmwareVersion = new byte[64];
                bySessionKey = new byte[64];
                byIV = new byte[32];
                byRes1 = new byte[2];
                byRes0 = new byte[4];
                byRes = new byte[283];
            }
        }




        // CN: CMS响应消息参数        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_RESPONSE_MSG_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szChildID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szLocalIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szResourceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szDomain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szIdentifier;
            public nint pInBuf;
            public uint dwInBufSize;
            public uint dwSequence;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;

            public void Init()
            {
                szChildID = new byte[32];
                szLocalIndex = new byte[32];
                szResourceType = new byte[64];
                szDomain = new byte[64];
                szIdentifier = new byte[64];
                byRes = new byte[256];
            }
        }

        // CN: IP结构体
        // EN: IP structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_IPADDRESS
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szIP;
            public short wPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;

            public void Init()
            {
                szIP = new char[128];
                byRes = new byte[6];
            }
        }

        // CN: DAS参数
        // EN: DAS parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_DAS_INFO
        {
            public OTAP_IPADDRESS struDevAddr;// CN: IP地址
            // EN: IP address
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] byDomain;// CN: 域名
            // EN: Domain name
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] byServerID;// CN: [in]ServerID. 用于标识不同的DAS服务
            // EN: [in]ServerID. Used to identify different DAS services
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 248, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;// CN: 保留字节大小
            // EN: Reserved byte size

            public void Init()
            {
                byDomain = new char[64];
                byServerID = new char[64];
                byRes = new byte[248];
            }
        }

        public delegate bool OTAP_CMS_RegisterCallback(int iUserID, uint dwDataType, nint pOutBuffer, uint dwOutLen, nint pInBuffer, uint dwInLen, nint pUserData);

        // CN: CMS监听参数
        // EN: CMS listening parameters
        public struct OTAP_CMS_LISTEN_PARAM
        {
            public OTAP_IPADDRESS struAddress;// CN: IP地址
            // EN: IP address
            public OTAP_CMS_RegisterCallback fnCB; // CN: 注册回调
            // EN: Register callback
            public nint pUserData;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_PRI_PUB_KEY
        {
            public nint pPriKeyBuf;        // CN: [OUT]
            // EN: [OUT]
            public nint pPubKeyBuf;        // CN: [OUT]
            // EN: [OUT]
            public uint dwPriKeyBufLen;      // CN: [IN]
            // EN: [IN]
            public uint dwPubKeyBufLen;      // CN: [IN]
            // EN: [IN]
            public uint dwPriKeyLen;         // CN: [OUT]
            // EN: [OUT]
            public uint dwPubKeyLen;         // CN: [OUT]
            // EN: [OUT]
            public uint dwPubKeyType;        // CN: [IN]
            // EN: [IN]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 124)]
            public byte[] byRes;             // CN: 保留字节
            // EN: Reserved bytes
        }

        // CN: 开启实时取流输入参数
        // EN: Start live streaming input parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STARTLIVESTREAMING_PARAM_IN
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] szServerSessionID;
            public uint dwChannel;
            public uint dwStreamType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] szServerIPv4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] szServerIPv6;
            public ushort wServerTcpPort;
            public ushort wServerUdpPort;
            public ushort wServerTLSPort;
            public byte byMode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes1;
            public nint pPubKey;
            public uint dwPubKeyLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 252, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;
        }

        // CN: CMS开启实时取流输出参数
        // EN: CMS start live streaming output parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STARTLIVESTREAMING_PARAM_OUT
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.I1)]
            public byte[] szDevSessionID;
            public int iAsyncHandle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] byRes;
        }

        // CN: CMS设备配置参数
        // EN: CMS device configuration parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_CONFIG_DEV_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szChildID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szLocalIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szResourceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szDomain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szIdentifier;
            public nint pInBuf;
            public nint pOutBuf;
            public uint dwInBufSize;
            public uint dwOutBufSize;
            public int iAsyncHandle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 252)]
            public byte[] byRes;

            public void Init()
            {
                szChildID = new char[32];
                szLocalIndex = new char[32];
                szResourceType = new char[64];
                szDomain = new char[64];
                szIdentifier = new char[64];

                byRes = new byte[252];
            }
        }

        // CN: CMS开启语音对讲输入参数
        // EN: CMS start voice intercom input parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STARTVOICE_IN
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
            public byte[] szServerSessionID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szServerIPv4;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szServerIPv6;

            public nint pPubKey;
            public uint dwPubKeyLen;
            public uint dwChannel;
            public ushort wServerTcpPort;
            public ushort wServerUdpPort;
            public ushort wServerTlsPort;
            public byte byMode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] byRes1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 236)]
            public byte[] byRes;
        }

        // CN: CMS开启语音对讲输出参数
        // EN: CMS start voice intercom output parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STARTVOICE_OUT
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szDevSessionID;

            public int iAudioType;
            public int iAsyncHandle;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)]
            public byte[] byRes;
        }

        // CN: CMS停止语音对讲参数
        // EN: CMS stop voice intercom parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STOPVOICE_PARAM
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szDevSessionID;
            // CN: 设备会话ID
            // EN: Device session ID
            public uint dwChannel;
            // CN: 通道号
            // EN: Channel number
            public int iAsyncHandle;
            // CN: 异步句柄
            // EN: Asynchronous handle
            public byte byMode;
            // CN: 模式
            // EN: Mode
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] byRes1;
            // CN: 保留字节
            // EN: Reserved bytes
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 148)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes
        }

        // CN: CMS停止实时取流参数
        // EN: CMS stop live streaming parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STOPLIVESTREAMING_PARAM
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szDevSessionID;
            // CN: 若实时取流是流媒体服务取流,此字段为 devsessionid
            // EN: If live streaming is from media service, this field is devsessionid
            public uint dwChannel;
            // CN: 实时取流通道
            // EN: Live streaming channel
            public int iAsyncHandle;
            public byte byMode;
            // CN: 0-视频实时流  1-音频实时流
            // EN: 0-Video live stream  1-Audio live stream
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
            public byte[] byRes1;
            public OTAP_CMS_InterfaceAsyncCallback fnCB;
            // CN: 接口使用的独立异步回调函数
            // EN: Independent asynchronous callback function used by the interface
            public nint pUserData;
            // CN: 接口使用的独立异步回调函数对应的用户指针
            // EN: User pointer corresponding to the independent asynchronous callback function used by the interface
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes
        }

        public delegate bool OTAP_CMS_StopLiveStreamingCallback(int iUserID, OTAP_CMS_STREAMING_MODE_ENUM enumStreamingMode, nint pParam);

        // CN: CMS接口异步回调信息
        // EN: CMS interface asynchronous callback information
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct OTAP_CMS_INTERFACE_ASYNC_CB_INFO
        {
            public int iUserID;
            // CN: [out] 用户ID
            // EN: [out] User ID
            public int iAsyncHandle;
            // CN: [out] 句柄
            // EN: [out] Handle
            public uint dwType;
            // CN: [out] 异步模式下的回调类型, 详见 ::tagOTAP_CMS_INTERFACE_ASYNC_CB_ENUM
            // EN: [out] Callback types in asynchronous mode, see details in ::tagOTAP_CMS_INTERFACE_ASYNC_CB_ENUM
            public uint dwErrorNo;
            // CN: [out] 错误码. 成功时为0
            // EN: [out] Error code. It is 0 for success
            public nint pOutBuffer;
            // CN: [out] 设备响应数据
            // EN: [out] Device response data
            public uint dwOutLen;
            // CN: [out] 设备响应数据的长度
            // EN: Length of the device response data
            public bool bSucc;
            // CN: [out] 成功标识
            // EN: Success identification
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes
        }

        public delegate void OTAP_CMS_InterfaceAsyncCallback(ref OTAP_CMS_INTERFACE_ASYNC_CB_INFO pData, nint pUserData);

        // CN: CMS ISAPI透传参数
        // EN: CMS ISAPI pass-through parameters
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct OTAP_CMS_ISAPI_PT_PARAM
        {
            public nint pRequestUrl;
            // CN: 请求URL
            // EN: Request URL
            public uint dwRequestUrlLen;
            // CN: 请求URL长度
            // EN: Request URL length
            public nint pCondBuffer;
            // CN: 条件缓冲区
            // EN: Condition buffer
            public uint dwCondSize;
            // CN: 条件缓冲区大小
            // EN: Condition buffer size
            public nint pInBuffer;
            // CN: 输入缓冲区
            // EN: Input buffer
            public uint dwInSize;
            // CN: 输入缓冲区大小
            // EN: Input buffer size
            public nint pOutBuffer;
            // CN: 输出缓冲区
            // EN: Output buffer
            public uint dwOutSize;
            // CN: 输出缓冲区大小
            // EN: Output buffer size
            public uint dwReturnedLen;
            // CN: 实际从设备接收到的数据长度
            // EN: Actual data length received from the device
            public uint dwRecvTimeOut;
            // CN: 接收超时时间
            // EN: Receive timeout
            public int iHandle;
            // CN: 异步句柄
            // EN: Asynchronous handle
            public OTAP_CMS_InterfaceAsyncCallback fnCB;
            // CN: 异步回调函数
            // EN: Asynchronous callback function
            public nint pUserData;
            // CN: 异步回调函数对应的用户指针
            // EN: User pointer corresponding to the asynchronous callback function
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes

            public void Init()
            {
                byRes = new byte[8];
            }
        }

        // CN: CMS订阅消息回调信息
        // EN: CMS subscribe message callback information
        public struct OTAP_CMS_SUBSCRIBE_MSG_CB_INFO
        {
            public uint dwType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szDevID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szChildID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szLocalIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szResourceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szDomain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] szIdentifier;
            public nint pOutBuf;
            public uint dwOutBufSize;
            public uint dwSequence;
            public nint pDeviceID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 252)]
            public byte[] byRes;
        }

        // CN: 设置订阅回调
        // EN: Set subscribe callback
        public delegate bool OTAP_CMS_SubscribeMsgCallback(int iUserID, ref OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam, nint pUserData);

        // CN: 设置订阅消息回调参数
        // EN: Set subscribe message callback parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_SET_SUBSCRIBEMSG_CB_PARAM
        {
            public OTAP_CMS_SubscribeMsgCallback fnCB;
            // CN: 订阅消息回调
            // EN: Subscribe message callback
            public nint pUserData;
            // CN: 用户指针
            // EN: User pointer
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes
        }

        // CN: 设置事件Topic过滤主题
        // EN: Set event Topic filter
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_SUBSCRIBEMSG_TOPIC_FILTER_PARAM
        {
            public nint pTopicFilter;
            // CN: Topic过滤
            // EN: Topic filter
            public uint dwTopicFilterLen;
            // CN: Topic过滤长度
            // EN: The length of pTopicFilter
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 124)]
            public byte[] byRes;
            // CN: 保留字节
            // EN: Reserved bytes

            public void Init()
            {
                byRes = new byte[124];
            }
        }

        // CN: OTAP消息订阅
        // EN: OTAP message subscription
        public enum OTAP_CMS_SUBSCRIBE_MSG_ENUM : uint
        {
            ENUM_OTAP_CMS_SET_CALLBACK_FUN = 0,
            ENUM_OTAP_CMS_SET_TOPIC_FILTER = 1,
            ENUM_OTAP_CMS_CANCEL_TOPIC_FILTER = 2
        }

        // CN: CMS开启设备报警参数
        // EN: CMS start device alarm parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STARTALARM_PARAM
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szServerIPv4;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szServerIPv6;
            public ushort wServerTcpPort;
            public ushort wServerTLSPort;
            public uint dwKeepAliveSec;
            public uint dwSubscribeInfoLen;
            public nint pSubscribeInfo;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byRes;
        }



        // CN: CMS简单存储响应类型
        // EN: CMS simple storage response type
        public enum OTAP_CMS_STORAGE_RESPONSE_TYPE_ENUM
        {
            ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY = 1,
            // CN: 输入参数  tagOTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM
            // EN: Input parameter  tagOTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM
            ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY = 2,
            // CN: 输入参数 空
            // EN: Input parameter Empty
            ENUM_OTAP_CMS_STORAGE_DOWNLOAD_QUERY_REPLY = 3
            // CN: 输入参数  tagOTAP_CMS_DOWNLOAD_OBJECT_INPUT_PARAM
            // EN: Input parameter  tagOTAP_CMS_DOWNLOAD_OBJECT_INPUT_PARAM
        }

        // CN: CMS存储订阅消息回调信息
        // EN: CMS storage subscription message callback information
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO
        {
            public uint dwType;
            // CN: 符合OTAP_CMS_STORAGE_SUBSCRIBE_TYPE_ENUM
            // EN: Conforms to OTAP_CMS_STORAGE_SUBSCRIBE_TYPE_ENUM
            public uint dwSequence;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szDevID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szChildID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szLocalIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szResourceType;
            public nint pOutBuf;
            public uint dwOutBufSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes1;
            public nint pDeviceID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 244)]
            public byte[] byRes;

            public void Init()
            {
                szDevID = new char[32];
                szChildID = new char[32];
                szLocalIndex = new char[32];
                szResourceType = new char[64];
                byRes1 = new byte[4];
                byRes = new byte[244];
            }
        }

        // CN: 存储消息回调
        // EN: Storage message callback
        public delegate void OTAP_CMS_StorageCallback(int iUserID, ref OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam, nint pUserData);

        // CN: CMS存储订阅回调参数
        // EN: CMS storage subscription callback parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM
        {
            public OTAP_CMS_StorageCallback fnCB;
            public nint pUserData;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] byRes;
        }

        // CN: CMS存储上传输出参数
        // EN: CMS storage upload output parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szDomain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szIdentifier;
            public byte byEncrypt;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 127)]
            public byte[] byRes;

            public void Init()
            {
                szDomain = new char[64];
                szIdentifier = new char[64];
                byRes = new byte[127];
            }
        }

        // CN: CMS响应简单存储消息
        // EN: CMS respond to simple storage message
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM
        {
            public uint dwType;
            public uint dwSequence;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szChildID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szLocalIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szResourceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szDomain;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szIdentifier;
            public nint pInBuf;
            public uint dwInBufSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byRes;

            public void Init()
            {
                szChildID = new char[32];
                szLocalIndex = new char[32];
                szResourceType = new char[64];
                szDomain = new char[64];
                szIdentifier = new char[64];
                byRes = new byte[256];
            }
        }

        // CN: CMS存储上传输入参数
        // EN: CMS storage upload input parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szStorageId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szBucketName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szObjectKey;
            public OTAP_IPADDRESS struAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szAccessKey;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szSecretKey;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            public char[] szRegion;
            public uint bHttps;
            public byte byEncrypt;
            public byte byMethod;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byRes2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes4;
            public nint pCustomHeaders;
            public nint pCustomUrl;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 104)]
            public byte[] byRes;

            public void Init()
            {
                szStorageId = new char[128];
                szBucketName = new char[64];
                szObjectKey = new char[128];
                szAccessKey = new char[128];
                szSecretKey = new char[128];
                szRegion = new char[132];
                byRes1 = new byte[2];
                byRes2 = new byte[32];
                byRes3 = new byte[4];
                byRes4 = new byte[4];
                byRes = new byte[104];
            }
        }

        // CN: CMS简单存储查询输出参数
        // EN: CMS simple storage query output parameters
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szStorageId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            public char[] szBucket;
            public uint dwResult;
            public byte byEncrypt;
            public byte byAlgorithm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 226)]
            public byte[] byRes;

            public void Init()
            {
                szStorageId = new char[128];
                szBucket = new char[64];
                byRes = new byte[226];
            }
        }

        // CN: CMS存储下载输入参数
        // EN: CMS storage download input parameters
        [StructLayout(LayoutKind.Sequential)]
        public struct OTAP_CMS_DOWNLOAD_OBJECT_INPUT_PARAM
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szBucketName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szObjectKey;
            public OTAP_IPADDRESS struAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szAccessKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szSecretKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegion;
            public uint dwExpires;
            public uint bHttps;
            public byte byEncrypt;
            public byte byAlgorithm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] byRes1;
            public nint pCustomUrl;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 176)]
            public byte[] byRes;
        }

        // CN: CMS存储下载输出参数
        // EN: CMS storage download output parameters
        [StructLayout(LayoutKind.Sequential)]
        public struct OTAP_CMS_DOWNLOAD_OBJECT_OUTPUT_PARAM
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szStorageId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] byRes;

            public void Init()
            {
                szStorageId = new char[128];
                byRes = new byte[128];
            }
        }

        // CN: CMS心跳参数
        [StructLayout(LayoutKind.Sequential)]
        public struct OTAP_CMS_SERVER_INFO
        {
            public uint dwKeepAliveSec; // CN: 心跳间隔秒数
            // EN: Heartbeat interval in seconds
            public uint dwTimeOutCount; // CN: 超时次数
            // EN: Timeout count
            public uint dwAMSKeepAliveSec; // CN: AMS心跳间隔秒数
            // EN: AMS heartbeat interval in seconds
            public uint dwAMSTimeOutCount; // CN: AMS超时次数
            // EN: AMS timeout count
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] byRes;
        }
        //typedef struct {
        //    unsigned int dwKeepAliveSec;
        //    unsigned int dwTimeOutCount;
        //    unsigned int dwAMSKeepAliveSec;
        //    unsigned int dwAMSTimeOutCount;
        //}
        //OTAP_CMS_SERVER_INFO;



        public HCOTAPCMS() { }
    }
}
