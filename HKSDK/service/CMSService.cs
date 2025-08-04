using GrpcService.Common;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using GrpcService.HKSDK.manager;

namespace GrpcService.HKSDK.service
{
    public class CMSService
    {
        private readonly ConcurrentDictionary<string, DeviceConnection> _devices = new();

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

                if (windowsLibraries.TryGetValue(libraryName, out string fileName))
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

                if (linuxLibraries.TryGetValue(libraryName, out string fileName))
                {
                    return Path.Combine(basePath, "Libs", "Linux", fileName);
                }
            }

            // 如果找不到对应的库文件，返回默认路径
            Console.WriteLine($"Warning: 未找到平台 {RuntimeInformation.OSDescription} 的库文件 {libraryName}");
            return Path.Combine(basePath, libraryName);
        }
        public void Cms_Init()
        {
            // 记录平台信息
            _logger.LogInformation($"当前平台: {HCOTAPCMS.GetPlatformInfo()}");
            _logger.LogInformation($"所需库文件: {string.Join(", ", HCOTAPCMS.GetRequiredLibraries())}");

            //设置libeay32库路径
            string libeayPath = GetPlatformLibraryPath("libcrypto");
            if (!HCOTAPCMS.OTAP_CMS_SetSDKInitCfg(HCOTAPCMS.ENUM_OTAP_CMS_INIT_CFG_LIBEAY_PATH, Marshal.StringToHGlobalAnsi(libeayPath)))
            {
                Console.WriteLine("ENUM_OTAP_CMS_INIT_CFG_LIBEAY_PATH failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }

            //设置ssleay32库路径
            string ssleayPath = GetPlatformLibraryPath("libssl");
            if (!HCOTAPCMS.OTAP_CMS_SetSDKInitCfg(HCOTAPCMS.ENUM_OTAP_CMS_INIT_CFG_SSLEAY_PATH, Marshal.StringToHGlobalAnsi(ssleayPath)))
            {
                Console.WriteLine("ENUM_OTAP_CMS_INIT_CFG_SSLEAY_PATH failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }

            //设置libiconv2库路径
            string libiconvPath = GetPlatformLibraryPath("libiconv2");
            if (!HCOTAPCMS.OTAP_CMS_SetSDKInitCfg(HCOTAPCMS.ENUM_OTAP_CMS_INIT_CFG_LIBICONV_PATH, Marshal.StringToHGlobalAnsi(libiconvPath)))
            {
                Console.WriteLine("ENUM_OTAP_CMS_INIT_CFG_LIBICONV_PATH failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }

            //设置zlib1库路径
            string zlibPath = GetPlatformLibraryPath("libz");
            if (!HCOTAPCMS.OTAP_CMS_SetSDKInitCfg(HCOTAPCMS.ENUM_OTAP_CMS_INIT_CFG_ZLIB_PATH, Marshal.StringToHGlobalAnsi(zlibPath)))
            {
                Console.WriteLine("ENUM_OTAP_CMS_INIT_CFG_ZLIB_PATH failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }

            //注册服务初始化
            if (!HCOTAPCMS.OTAP_CMS_Init())
            {
                Console.WriteLine("OTAP_CMS_Init failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }
            else
            {
                Console.WriteLine("OTAP_CMS_Init succ!");
            }

            HCOTAPCMS.OTAP_CMS_SetLogToFile(3, CMSServiceHelpers.sCurPath + "/SdkLog", false);
        }

        public void Cms_SubscribeMsg()
        {
            CMSServiceHelpers.OTAP_SubscribeMsgCallback_Func ??= new HCOTAPCMS.OTAP_CMS_SubscribeMsgCallback(FSubscribeMsgCallback);

            CMSServiceHelpers.
                        subscribeMsgParam.fnCB = CMSServiceHelpers.OTAP_SubscribeMsgCallback_Func;

            int size = Marshal.SizeOf(CMSServiceHelpers.subscribeMsgParam);
            IntPtr ptrSubscribeMsgParam = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(CMSServiceHelpers.subscribeMsgParam, ptrSubscribeMsgParam, false);

            if (!HCOTAPCMS.OTAP_CMS_SubscribeMsg(HCOTAPCMS.OTAP_CMS_SUBSCRIBE_MSG_ENUM.ENUM_OTAP_CMS_SET_CALLBACK_FUN, ptrSubscribeMsgParam))
            {
                Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_CALLBACK_FUN failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }
            else
            {
                Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_CALLBACK_FUN succ!");
            }

            //设置主题过滤器
            HCOTAPCMS.OTAP_CMS_SUBSCRIBEMSG_TOPIC_FILTER_PARAM struTopicFilter = new();
            struTopicFilter.Init();

            string szTopicFilter = "#model#\r\n";//订阅的主题
            struTopicFilter.dwTopicFilterLen = (uint)szTopicFilter.Length;
            struTopicFilter.pTopicFilter = Marshal.StringToHGlobalAnsi(szTopicFilter);

            size = Marshal.SizeOf(struTopicFilter);
            IntPtr ptrStruTopicFilter = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(struTopicFilter, ptrStruTopicFilter, false);

            if (!HCOTAPCMS.OTAP_CMS_SubscribeMsg(HCOTAPCMS.OTAP_CMS_SUBSCRIBE_MSG_ENUM.ENUM_OTAP_CMS_SET_TOPIC_FILTER, ptrStruTopicFilter))
            {
                Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_TOPIC_FILTER failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }
            else
            {
                Console.WriteLine("OTAP_CMS_SubscribeMsg ENUM_OTAP_CMS_SET_TOPIC_FILTER succ!");
            }
        }

        public void Cms_Startlisten()
        {
            CMSServiceHelpers.cmsListenParam.struAddress.Init();

            string strCmsListenIp = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "CmsServerIP");
            strCmsListenIp.CopyTo(0, CMSServiceHelpers.cmsListenParam.struAddress.szIP, 0, strCmsListenIp.Length);
            string strCmsListenPort = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "CmsServerPort");
            CMSServiceHelpers.cmsListenParam.struAddress.wPort = short.Parse(strCmsListenPort);

            CMSServiceHelpers.OTAP_REGISTER_Func ??= new HCOTAPCMS.OTAP_CMS_RegisterCallback(FRegisterCallBack);

            CMSServiceHelpers.cmsListenParam.fnCB = CMSServiceHelpers.OTAP_REGISTER_Func;
            CMSServiceHelpers.cmsListenParam.byRes = new byte[128];
            CMSServiceHelpers.
                        CmsListenHandle = HCOTAPCMS.OTAP_CMS_StartListen(ref CMSServiceHelpers.cmsListenParam);
            if (CMSServiceHelpers.CmsListenHandle < 0)
            {
                Console.WriteLine("OTAP_CMS_StartListen failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }
            else
            {
                Console.WriteLine("OTAP_CMS_StartListen succ, ip:" + strCmsListenIp + ", port:" + strCmsListenPort);
            }
        }

        public void Cms_SetSubscribeStorage()
        {
            CMSServiceHelpers.OTAP_CMS_StorageCallback_Func ??= new HCOTAPCMS.OTAP_CMS_StorageCallback(FStorageCallback);

            CMSServiceHelpers.struStorageCBParam.fnCB = CMSServiceHelpers.OTAP_CMS_StorageCallback_Func;

            if (!HCOTAPCMS.OTAP_CMS_SubscribeStorageMsg(ref CMSServiceHelpers.struStorageCBParam))
            {
                Console.WriteLine("OTAP_CMS_SubscribeStorageMsg failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
            }
            else
            {
                Console.WriteLine("OTAP_CMS_SubscribeStorageMsg succ!");
            }

        }

        public void Cms_Stoplisten()
        {
            if (CMSServiceHelpers.CmsListenHandle > 0)
            {
                if (!HCOTAPCMS.OTAP_CMS_StopListen(CMSServiceHelpers.CmsListenHandle))
                {
                    Console.WriteLine("OTAP_CMS_StopListen failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                }
                else
                {
                    CMSServiceHelpers.CmsListenHandle = -1;
                    Console.WriteLine("OTAP_CMS_StopListen succ");
                }
            }
        }

        public bool FRegisterCallBack(int lUserID, uint dwDataType, IntPtr pOutBuffer, uint dwOutLen, IntPtr pInBuffer, uint dwInLen, IntPtr pUserData)
        {
            Console.WriteLine("FRegisterCallBack, dwDataType:" + dwDataType + ", lUserID:" + lUserID);

            HCOTAPCMS.OTAP_CMS_DEV_REG_INFO struDevInfo = new();
            struDevInfo.Init();
            struDevInfo.struDevAddr.Init();
            struDevInfo.struRegAddr.Init();
            if (pOutBuffer != IntPtr.Zero)
            {
                struDevInfo = (HCOTAPCMS.OTAP_CMS_DEV_REG_INFO)Marshal.PtrToStructure(pOutBuffer, typeof(HCOTAPCMS.OTAP_CMS_DEV_REG_INFO));
            }
            string strDeviceID = Encoding.Default.GetString(struDevInfo.byDeviceID).TrimEnd('\0');

            if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_ON || dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_ADDRESS_CHANGED || dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_DAS_REREGISTER)
            {
                //均视为设备上线
                Console.WriteLine("ENUM_OTAP_CMS_DEV_ON, lUserID:" + lUserID + ", DeviceID:" + strDeviceID);
                //设备上线时，注册设备连接信息
                
                if (pInBuffer != IntPtr.Zero && dwInLen > 0)
                {
                    HCOTAPCMS.OTAP_CMS_SERVER_INFO struServerInfo = (HCOTAPCMS.OTAP_CMS_SERVER_INFO)Marshal.PtrToStructure(pInBuffer, typeof(HCOTAPCMS.OTAP_CMS_SERVER_INFO));
                    struServerInfo.dwKeepAliveSec = 60; //心跳间隔时间,单位秒    
                    struServerInfo.dwTimeOutCount = 6; //心跳超时次数
                    Console.WriteLine("ENUM_OTAP_CMS_DEV_ON, KeepAliveSec:" + struServerInfo.dwKeepAliveSec + ", TimeOutCount:" + struServerInfo.dwTimeOutCount);
                }
            }
            else if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_DAS_PINGREQ_CALLBACK)
            {
                Console.WriteLine("ENUM_OTAP_CMS_DEV_HEARTBEAT, lUserID:" + lUserID + ", DeviceID:" + strDeviceID);
                _devices[strDeviceID].LastHeartbeat = DateTime.Now;
            }
            else if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_OFF || dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_SESSIONKEY_ERROR ||
                    dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_DAS_OTAPKEY_ERROR)
            {
                //均视为设备下线
                _devices[strDeviceID].IsConnected = false;
                Console.WriteLine("ENUM_OTAP_CMS_DEV_OFF, lUserID:" + lUserID + ", DeviceID:" + strDeviceID);
            }
            else if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_AUTH)
            {
                string OTAPKey = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "OTAPKey");
                byte[] byTemp = Encoding.Default.GetBytes(OTAPKey);

                byte[] byOTAPKey = new byte[32];
                byTemp.CopyTo(byOTAPKey, 0);
                Marshal.Copy(byOTAPKey, 0, pInBuffer, 32);
                Console.WriteLine("ENUM_OTAP_CMS_DEV_AUTH, DeviceID:" + strDeviceID + ",OTAPKey:" + OTAPKey);
            }
            else if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DEV_SESSIONKEY)
            {
                //ENUM_OTAP_CMS_DEV_SESSIONKEY负载均衡模式下, 由LBS触发
                //需要将SessionKey传递给LBS
            }
            else if (dwDataType == HCOTAPCMS.ENUM_OTAP_CMS_DAS_REQ)
            {

                HCOTAPCMS.OTAP_CMS_DAS_INFO struCmsDasInfo = new();
                struCmsDasInfo.Init();
                struCmsDasInfo.struDevAddr.Init();

                string DasServerIP = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "DasServerIP");
                DasServerIP.CopyTo(0, struCmsDasInfo.struDevAddr.szIP, 0, DasServerIP.Length);
                string domain = "test.ys7.com";
                domain.CopyTo(0, struCmsDasInfo.byDomain, 0, domain.Length);
                string DasServerPort = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "DasServerPort");
                struCmsDasInfo.struDevAddr.wPort = short.Parse(DasServerPort);
                string strServerID = "das_" + DasServerIP + "_" + DasServerPort;
                strServerID.CopyTo(0, struCmsDasInfo.byServerID, 0, strServerID.Length);

                Marshal.StructureToPtr(struCmsDasInfo, pInBuffer, false);
                Console.WriteLine("ENUM_OTAP_CMS_DAS_REQ, byServerID:" + strServerID);
            }

            return true;
        }

        public void FStorageCallback(int iUserID, ref HCOTAPCMS.OTAP_CMS_STORAGE_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        {
            if (pParam.dwType == HCOTAPCMS.ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY)  //设备->平台,订阅上传查询消息
            {
                HCOTAPCMS.OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM struUpload = new();
                struUpload.Init();
                if (pParam.pOutBuf != IntPtr.Zero)
                {
                    struUpload = (HCOTAPCMS.OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM)Marshal.PtrToStructure(pParam.pOutBuf, typeof(HCOTAPCMS.OTAP_CMS_UPLOAD_OBJECT_OUTPUT_PARAM));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, pParam.szDevID:" + new String(pParam.szDevID).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, pParam.szChildID:" + new String(pParam.szChildID).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, pParam.szLocalIndex:" + new String(pParam.szLocalIndex).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, pParam.szResourceType:" + new String(pParam.szResourceType).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, pParam.dwSequence:" + pParam.dwSequence);
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, struUpload.szDomain:" + new String(struUpload.szDomain).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, struUpload.szIdentifier:" + new String(struUpload.szIdentifier).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY, struUpload.byEncrypt:" + struUpload.byEncrypt);

                    //此处szStorageId和szObjectKey三方需要自行关联好
                    HCOTAPCMS.OTAP_CMS_UPLOAD_OBJECT_INPUT_PARAM struUploadObjInputParam = new();
                    struUploadObjInputParam.Init();
                    struUploadObjInputParam.struAddress.Init();
                    //SS存储服务器IP和PORT
                    string PicServerIP = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "PicServerIP");
                    PicServerIP.CopyTo(0, struUploadObjInputParam.struAddress.szIP, 0, PicServerIP.Length);
                    string PicServerPort = CommonMethod.ReadConfigValue(CMSServiceHelpers.configPath, "PicServerPort");
                    struUploadObjInputParam.struAddress.wPort = short.Parse(PicServerPort);
                    //关键参数, szStorageId和szObjectKey用户自行定义, 可以为随机数或UUID，建议用同一个
                    Guid uuid = Guid.NewGuid();
                    string szStorageId = uuid.ToString();
                    szStorageId.CopyTo(0, struUploadObjInputParam.szStorageId, 0, szStorageId.Length);
                    string szObjectKey = uuid.ToString();
                    szObjectKey.CopyTo(0, struUploadObjInputParam.szObjectKey, 0, szObjectKey.Length);
                    Console.WriteLine("自定义szStorageId:" + szStorageId + ",自定义szObjectKey:" + szObjectKey);
                    //和存储服务启动监听设置的Bucket一致
                    string szBucket = "otapsstest";
                    szBucket.CopyTo(0, struUploadObjInputParam.szBucketName, 0, szBucket.Length);
                    //和存储服务启动监听设置的AccessKey一致
                    string szAccessKey = "HCSx5ZO0Ik419x4P23L5JerQ475O213";
                    szAccessKey.CopyTo(0, struUploadObjInputParam.szAccessKey, 0, szAccessKey.Length);
                    //和存储服务启动监听设置的SecretKey一致
                    string szSecretKey = "4y8f4V9xn5454b919xaT8Bv2274r0O25";
                    szSecretKey.CopyTo(0, struUploadObjInputParam.szSecretKey, 0, szSecretKey.Length);
                    //和存储服务启动监听设置的RegionCode一致
                    string szRegion = "Local";
                    szRegion.CopyTo(0, struUploadObjInputParam.szRegion, 0, szRegion.Length);
                    struUploadObjInputParam.bHttps = 0;//是否使用HTTPS. 0-使用HTTP; 1-使用HTTPS
                    struUploadObjInputParam.byEncrypt = 0;//数据是否加密. 0-不进行数据加密; 1-进行数据加密

                    HCOTAPCMS.OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM struStorageResponseMsg = new();
                    struStorageResponseMsg.Init();
                    struStorageResponseMsg.szChildID = pParam.szChildID;
                    struStorageResponseMsg.szLocalIndex = pParam.szLocalIndex;
                    struStorageResponseMsg.szResourceType = pParam.szResourceType;
                    struStorageResponseMsg.dwSequence = pParam.dwSequence;
                    struStorageResponseMsg.dwType = HCOTAPCMS.ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY;
                    struStorageResponseMsg.pInBuf = Marshal.AllocHGlobal(Marshal.SizeOf(struUploadObjInputParam));
                    Marshal.StructureToPtr(struUploadObjInputParam, struStorageResponseMsg.pInBuf, false);
                    struStorageResponseMsg.dwInBufSize = (uint)Marshal.SizeOf(struUploadObjInputParam);

                    if (!HCOTAPCMS.OTAP_CMS_ResponseStorageMsg(iUserID, ref struStorageResponseMsg))
                    {
                        Console.WriteLine("OTAP_CMS_ResponseStorageMsg ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                    }
                    else
                    {
                        Console.WriteLine("OTAP_CMS_ResponseStorageMsg ENUM_OTAP_CMS_STORAGE_UPLOAD_QUERY_REPLY succ!");
                    }
                }
            }
            else if (pParam.dwType == HCOTAPCMS.ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT)//设备->平台,订阅上传结果上报消息
            {
                HCOTAPCMS.OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM struReport = new();
                struReport.Init();
                if (pParam.pOutBuf != IntPtr.Zero)
                {
                    struReport = (HCOTAPCMS.OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM)Marshal.PtrToStructure(pParam.pOutBuf, typeof(HCOTAPCMS.OTAP_CMS_REPORT_OBJECT_OUTPUT_PARAM));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, pParam.szDevID:" + new String(pParam.szDevID).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, pParam.szChildID:" + new String(pParam.szChildID).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, pParam.szLocalIndex:" + new String(pParam.szLocalIndex).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, pParam.szResourceType:" + new String(pParam.szResourceType).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, pParam.dwSequence:" + pParam.dwSequence);
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, struReport.szStorageId:" + new String(struReport.szStorageId).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, struReport.szBucket:" + new String(struReport.szBucket).TrimEnd('\0'));
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, struReport.dwResult:" + struReport.dwResult);
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, struReport.byEncrypt:" + struReport.byEncrypt);
                    Console.WriteLine("ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT, struReport.byAlgorithm:" + struReport.byAlgorithm);

                    HCOTAPCMS.OTAP_CMS_STORAGE_RESPONSE_MSG_PARAM struStorageResponseMsg = new();
                    struStorageResponseMsg.Init();
                    struStorageResponseMsg.szChildID = pParam.szChildID;
                    struStorageResponseMsg.szLocalIndex = pParam.szLocalIndex;
                    struStorageResponseMsg.szResourceType = pParam.szResourceType;
                    struStorageResponseMsg.dwSequence = pParam.dwSequence;
                    struStorageResponseMsg.dwType = HCOTAPCMS.ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY;
                    struStorageResponseMsg.pInBuf = IntPtr.Zero;
                    struStorageResponseMsg.dwInBufSize = 0;

                    if (!HCOTAPCMS.OTAP_CMS_ResponseStorageMsg(iUserID, ref struStorageResponseMsg))
                    {
                        Console.WriteLine("OTAP_CMS_ResponseStorageMsg ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                    }
                    else
                    {
                        Console.WriteLine("OTAP_CMS_ResponseStorageMsg ENUM_OTAP_CMS_STORAGE_UPLOAD_REPORT_REPLY succ!");
                    }
                }
            }
        }

        public bool FSubscribeMsgCallback(int iUserID, ref HCOTAPCMS.OTAP_CMS_SUBSCRIBE_MSG_CB_INFO pParam, IntPtr pUserData)
        {
            Console.WriteLine("FSubscribeMsgCallback in, iUserID:" + iUserID + ",dwType: " + pParam.dwType);
            switch (pParam.dwType)
            {
                //属性上报
                case HCOTAPCMS.ENUM_OTAP_CMS_ATTRIBUTE_REPORT_MODEL:
                    string DeviceID = Encoding.UTF8.GetString(pParam.szDevID).TrimEnd('\0');
                    string szDomain = Encoding.UTF8.GetString(pParam.szDomain).TrimEnd('\0');
                    string szIdentifier = Encoding.UTF8.GetString(pParam.szIdentifier).TrimEnd('\0');
                    Console.WriteLine("dwType:" + pParam.dwType + ", DeviceID:" + DeviceID + ", 功能领域:" + szDomain + ", 操作标识:" + szIdentifier);

                    byte[] byOutbuffer = new byte[pParam.dwOutBufSize];
                    Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
                    string strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');
                    Console.WriteLine("属性上报报文:" + strOutbuffer);
                    break;
                //上行操作
                case HCOTAPCMS.ENUM_OTAP_CMS_SERVICE_QUERY_MODEL:
                    DeviceID = Encoding.UTF8.GetString(pParam.szDevID).TrimEnd('\0');
                    szDomain = Encoding.UTF8.GetString(pParam.szDomain).TrimEnd('\0');
                    szIdentifier = Encoding.UTF8.GetString(pParam.szIdentifier).TrimEnd('\0');
                    Console.WriteLine("dwType:" + pParam.dwType + ", DeviceID:" + DeviceID + ", 功能领域:" + szDomain + ", 操作标识:" + szIdentifier);

                    byOutbuffer = new byte[pParam.dwOutBufSize];
                    Marshal.Copy(pParam.pOutBuf, byOutbuffer, 0, (int)pParam.dwOutBufSize);
                    strOutbuffer = Encoding.UTF8.GetString(byOutbuffer).TrimEnd('\0');
                    Console.WriteLine("上行操作报文:" + strOutbuffer);
                    break;
                //CMS报警
                case HCOTAPCMS.ENUM_OTAP_CMS_EVENT_REPORT_MODEL:
                    DeviceID = Encoding.UTF8.GetString(pParam.szDevID).TrimEnd('\0');
                    szDomain = Encoding.UTF8.GetString(pParam.szDomain).TrimEnd('\0');
                    szIdentifier = Encoding.UTF8.GetString(pParam.szIdentifier).TrimEnd('\0');
                    Console.WriteLine("dwType:" + pParam.dwType + ", DeviceID:" + DeviceID + ", 功能领域:" + szDomain + ", 操作标识:" + szIdentifier);
                    if (pParam.pOutBuf != IntPtr.Zero && pParam.dwOutBufSize > 0)
                    {
                        HCOTAPCMS.OTAP_AMS_ALARM_MSG struAlarmMsg = (HCOTAPCMS.OTAP_AMS_ALARM_MSG)Marshal.PtrToStructure(pParam.pOutBuf, typeof(HCOTAPCMS.OTAP_AMS_ALARM_MSG));
                        string strAlarmInfoBuf = "";
                        if (struAlarmMsg.pAlarmInfoBuf != IntPtr.Zero && struAlarmMsg.dwAlarmInfoLen > 0)
                        {
                            byte[] byAlarmInfoBuf = new byte[struAlarmMsg.dwAlarmInfoLen];
                            Marshal.Copy(struAlarmMsg.pAlarmInfoBuf, byAlarmInfoBuf, 0, (int)struAlarmMsg.dwAlarmInfoLen);
                            strAlarmInfoBuf = Encoding.UTF8.GetString(byAlarmInfoBuf).TrimEnd('\0');
                        }
                        Console.WriteLine("OTAP报警的TOPIC:" + struAlarmMsg.szAlarmTopic + ", CMS事件报文:" + strAlarmInfoBuf);
                    }
                    break;
                default:
                    break;
            }
            return true;
        }

    }

    // 设备连接信息模型
    public class DeviceConnection
    {
        public string? DeviceId { get; set; }
        public string? DeviceIP { get; set; }
        public int? DevicePort { get; set; }
        public int? UserId { get; set; }
        public bool? IsConnected { get; set; }
        public DateTime? LastHeartbeat { get; set; }
    }
}
