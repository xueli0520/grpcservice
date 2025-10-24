using GrpcService.HKSDK;
using System.Runtime.InteropServices;
using System.Text;

namespace GrpcService.Infrastructure
{
    internal static class CMSServiceHelpers
    {
        public static HCISUPCMS.NET_EHOME_CMS_LISTEN_PARAM cmsListenParam = new();
        public static HCISUPCMS.DEVICE_REGISTER_CB? ISUP_REGISTER_Func = null;
        public static HCISUPAlarm.NET_EHOME_ALARM_LISTEN_PARAM struAlarmListenParam = new();
        public static HCISUPAlarm.EHomeMsgCallBack? AlarmMsgCallBack_Func = null;
        public static int CmsListenHandle = -1;
        public static int AlarmListenHandle = -1;

        public static readonly string sCurPath = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// ISAPI透传接口封装
        /// </summary>
        public static string Cms_ISAPIPassThrough(int lUserID, string url, string inputStr, ILogger? logger = null)
        {
            HCISUPCMS.NET_EHOME_PTXML_PARAM struParam = new();
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

                if (!HCISUPCMS.NET_ECMS_ISAPIPassThrough(lUserID, ref struParam))
                {
                    var errorCode = HCISUPCMS.NET_ECMS_GetLastError();
                    logger?.LogError("NET_ECMS_ISAPIPassThrough failed, error: {ErrorCode}", errorCode);
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
                        strOutBuffer = strOutBuffer[..nullIndex];
                    }
                }
                else
                {
                    int nullIndex = strOutBuffer.IndexOf('\0');
                    if (nullIndex != -1)
                    {
                        strOutBuffer = strOutBuffer[..nullIndex];
                    }
                }

                logger?.LogDebug("NET_ECMS_ISAPIPassThrough succ, response length: {Length}", strOutBuffer.Length);
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