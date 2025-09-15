using GrpcService.HKSDK;
using System.Runtime.InteropServices;
using System.Text;

namespace GrpcService.Infrastructure
{
    internal static class CMSServiceHelpers
    {
        public static HCOTAPCMS.OTAP_CMS_LISTEN_PARAM cmsListenParam = new();
        public static HCOTAPCMS.OTAP_CMS_RegisterCallback? OTAP_REGISTER_Func = null;
        public static HCOTAPCMS.OTAP_CMS_SubscribeMsgCallback? OTAP_SubscribeMsgCallback_Func = null;
        public static HCOTAPCMS.OTAP_CMS_StorageCallback? OTAP_CMS_StorageCallback_Func = null;
        public static HCOTAPCMS.OTAP_SET_SUBSCRIBEMSG_CB_PARAM subscribeMsgParam = new();
        public static HCOTAPCMS.OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM struStorageCBParam = new();
        public static int CmsListenHandle = -1;

        public static readonly string sCurPath = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// ISAPI透传接口封装
        /// </summary>
        public static string Cms_ISAPIPassThrough(int lUserID, string url, string inputStr, ILogger? logger = null)
        {
            HCOTAPCMS.OTAP_CMS_ISAPI_PT_PARAM struParam = new();
            struParam.Init();

            IntPtr requestUrlPtr = IntPtr.Zero;
            IntPtr inBufferPtr = IntPtr.Zero;
            IntPtr outBufferPtr = IntPtr.Zero;

            try
            {
                // 输入ISAPI协议命令
                uint dwRequestUrlLen = (uint)url.Length;
                requestUrlPtr = Marshal.StringToHGlobalAnsi(url);
                struParam.pRequestUrl = requestUrlPtr;
                struParam.dwRequestUrlLen = dwRequestUrlLen;
                logger?.LogDebug("透传URL: {Url}", url);

                // 输入XML/JSON报文, GET命令输入报文为空
                if (!string.IsNullOrEmpty(inputStr))
                {
                    byte[] byInputParam = Encoding.UTF8.GetBytes(inputStr);
                    int iXMLInputLen = byInputParam.Length;
                    inBufferPtr = Marshal.AllocHGlobal(iXMLInputLen);
                    Marshal.Copy(byInputParam, 0, inBufferPtr, iXMLInputLen);
                    struParam.pInBuffer = inBufferPtr;
                    struParam.dwInSize = (uint)byInputParam.Length;
                    logger?.LogDebug("透传输入报文: {Input}", inputStr.Length > 500 ? string.Concat(inputStr.AsSpan(0, 500), "...") : inputStr);
                }

                outBufferPtr = Marshal.AllocHGlobal(20 * 1024);
                struParam.pOutBuffer = outBufferPtr;
                struParam.dwOutSize = 20 * 1024;

                if (!HCOTAPCMS.OTAP_CMS_ISAPIPassThrough(lUserID, ref struParam))
                {
                    var errorCode = HCOTAPCMS.OTAP_CMS_GetLastError();
                    logger?.LogError("OTAP_CMS_ISAPIPassThrough failed, error: {ErrorCode}", errorCode);
                    return string.Empty;
                }

                uint iXMSize = struParam.dwOutSize;
                byte[] managedArray = new byte[iXMSize];
                Marshal.Copy(struParam.pOutBuffer, managedArray, 0, (int)iXMSize);
                string strOutBuffer = Encoding.UTF8.GetString(managedArray);

                if (strOutBuffer.Contains("multipart/form-data"))
                {
                    int nullIndex = strOutBuffer.IndexOf("--MIME_boundary--");
                    if (nullIndex != -1)
                    {
                        strOutBuffer = strOutBuffer.Substring(0, nullIndex);
                    }
                }
                else
                {
                    int nullIndex = strOutBuffer.IndexOf('\0');
                    if (nullIndex != -1)
                    {
                        strOutBuffer = strOutBuffer.Substring(0, nullIndex);
                    }
                }

                logger?.LogDebug("OTAP_CMS_ISAPIPassThrough succ, response length: {Length}", strOutBuffer.Length);
                return strOutBuffer;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "ISAPI透传异常: URL={Url}", url);
                return string.Empty;
            }
            finally
            {
                // 清理资源
                if (requestUrlPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(requestUrlPtr);
                if (inBufferPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(inBufferPtr);
                if (outBufferPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(outBufferPtr);
            }
        }

        /// <summary>
        /// 设备配置下行操作
        /// </summary>
        public static string Cms_SetConfigDev(
            int lUserID,
            HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM enumMsg,
            string sDomain,
            string sIdentifier,
            string inputData,
            ILogger? logger = null)
        {
            HCOTAPCMS.OTAP_CMS_CONFIG_DEV_PARAM struConfigParam = new();
            struConfigParam.Init();

            IntPtr inBufPtr = IntPtr.Zero;
            IntPtr outBufPtr = IntPtr.Zero;

            try
            {
                // 子设备ID,设备本身固定为global
                string sChildID = "global";
                sChildID.CopyTo(0, struConfigParam.szChildID, 0, Math.Min(sChildID.Length, struConfigParam.szChildID.Length - 1));

                // 设备本地资源标识,设备本身固定为0
                string sLocalIndex = "0";
                sLocalIndex.CopyTo(0, struConfigParam.szLocalIndex, 0, Math.Min(sLocalIndex.Length, struConfigParam.szLocalIndex.Length - 1));

                // 设备资源类型,设备本身固定为global
                string sResourceType = "global";
                sResourceType.CopyTo(0, struConfigParam.szResourceType, 0, Math.Min(sResourceType.Length, struConfigParam.szResourceType.Length - 1));

                // 功能领域
                sDomain.CopyTo(0, struConfigParam.szDomain, 0, Math.Min(sDomain.Length, struConfigParam.szDomain.Length - 1));

                // 功能标识/属性标识
                sIdentifier.CopyTo(0, struConfigParam.szIdentifier, 0, Math.Min(sIdentifier.Length, struConfigParam.szIdentifier.Length - 1));

                // 输入参数
                if (!string.IsNullOrEmpty(inputData))
                {
                    byte[] byInputParam = Encoding.UTF8.GetBytes(inputData);
                    int iXMLInputLen = byInputParam.Length;
                    inBufPtr = Marshal.AllocHGlobal(iXMLInputLen);
                    Marshal.Copy(byInputParam, 0, inBufPtr, iXMLInputLen);
                    struConfigParam.pInBuf = inBufPtr;
                    struConfigParam.dwInBufSize = (uint)byInputParam.Length;
                    logger?.LogDebug("下行操作输入报文: {Input}", inputData.Length > 500 ? inputData.Substring(0, 500) + "..." : inputData);
                }

                outBufPtr = Marshal.AllocHGlobal(20 * 1024);
                struConfigParam.pOutBuf = outBufPtr;
                struConfigParam.dwOutBufSize = 20 * 1024;

                if (!HCOTAPCMS.OTAP_CMS_ConfigDev(lUserID, enumMsg, ref struConfigParam))
                {
                    var errorCode = HCOTAPCMS.OTAP_CMS_GetLastError();
                    logger?.LogError("OTAP_CMS_ConfigDev failed, error: {ErrorCode}, Domain: {Domain}, Identifier: {Identifier}",
                        errorCode, sDomain, sIdentifier);
                    return string.Empty;
                }

                uint iXMSize = struConfigParam.dwOutBufSize;
                byte[] managedArray = new byte[iXMSize];
                Marshal.Copy(struConfigParam.pOutBuf, managedArray, 0, (int)iXMSize);
                string strOutBuffer = Encoding.UTF8.GetString(managedArray).TrimEnd('\0');

                int nullIndex = strOutBuffer.IndexOf('\0');
                if (nullIndex != -1)
                {
                    strOutBuffer = strOutBuffer.Substring(0, nullIndex);
                }

                logger?.LogDebug("OTAP_CMS_ConfigDev succ, Domain: {Domain}, Identifier: {Identifier}, Response: {Response}",
                    sDomain, sIdentifier, strOutBuffer.Length > 200 ? strOutBuffer.Substring(0, 200) + "..." : strOutBuffer);

                return strOutBuffer;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "设备配置下行操作异常: Domain={Domain}, Identifier={Identifier}", sDomain, sIdentifier);
                return string.Empty;
            }
            finally
            {
                // 清理资源
                if (inBufPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(inBufPtr);
                if (outBufPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(outBufPtr);
            }
        }

        /// <summary>
        /// 获取系统信息
        /// </summary>
        public static Dictionary<string, object> GetSystemInfo()
        {
            return new Dictionary<string, object>
            {
                ["current_path"] = sCurPath,
                ["listen_handle"] = CmsListenHandle,
                ["platform"] = Environment.OSVersion.Platform.ToString(),
                ["os_version"] = Environment.OSVersion.VersionString,
                ["machine_name"] = Environment.MachineName,
                ["processor_count"] = Environment.ProcessorCount,
                ["working_set"] = Environment.WorkingSet,
                ["gc_memory"] = GC.GetTotalMemory(false),
                ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }
}