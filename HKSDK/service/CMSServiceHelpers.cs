using GrpcService.HKSDK.service;
using System.Runtime.InteropServices;
using System.Text;

namespace GrpcService.HKSDK.service
{
    internal static class CMSServiceHelpers
    {

        public static HCOTAPCMS.OTAP_CMS_LISTEN_PARAM cmsListenParam = new();
        public static HCOTAPCMS.OTAP_CMS_RegisterCallback OTAP_REGISTER_Func = null;
        public static HCOTAPCMS.OTAP_CMS_SubscribeMsgCallback OTAP_SubscribeMsgCallback_Func = null;
        public static HCOTAPCMS.OTAP_CMS_StorageCallback OTAP_CMS_StorageCallback_Func = null;
        public static HCOTAPCMS.OTAP_SET_SUBSCRIBEMSG_CB_PARAM subscribeMsgParam = new();
        public static HCOTAPCMS.OTAP_CMS_STORAGE_SUBSCRIBE_CB_PARAM struStorageCBParam = new();
        public static int CmsListenHandle = -1;     //注册监听句柄

        public static readonly string sCurPath = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string configPath = sCurPath + "/config.properties";

        public static void Cms_ISAPIPassThrough(int lUserID, string url, string inputStr)//该函数封装了SDK透传接口OTAP_CMS_ISAPIPassThrough
        {
            HCOTAPCMS.OTAP_CMS_ISAPI_PT_PARAM struParam = new();
            struParam.Init();

            //输入ISAPI协议命令
            uint dwRequestUrlLen = (uint)url.Length;
            struParam.pRequestUrl = Marshal.StringToHGlobalAnsi(url);
            struParam.dwRequestUrlLen = dwRequestUrlLen;
            Console.WriteLine("透传URL:" + url);

            //输入XML/JSON报文, GET命令输入报文为空
            if (inputStr != "")
            {
                byte[] byInputParam = Encoding.UTF8.GetBytes(inputStr);
                int iXMLInputLen = byInputParam.Length;

                struParam.pInBuffer = Marshal.AllocHGlobal(iXMLInputLen);
                Marshal.Copy(byInputParam, 0, struParam.pInBuffer, iXMLInputLen);
                struParam.dwInSize = (uint)byInputParam.Length;

                Console.WriteLine("透传输入报文:" + inputStr);
            }

            struParam.pOutBuffer = Marshal.AllocHGlobal(20 * 1024);    //输出缓冲区，如果接口调用失败提示错误码43，需要增大输出缓冲区
            struParam.dwOutSize = 20 * 1024;

            //struParam.dwRecvTimeOut = 10000;    //超时时间，单位毫秒，默认5s

            if (!HCOTAPCMS.OTAP_CMS_ISAPIPassThrough(lUserID, ref struParam))
            {
                Console.WriteLine("OTAP_CMS_ISAPIPassThrough failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                return;
            }

            uint iXMSize = struParam.dwOutSize;
            byte[] managedArray = new byte[iXMSize];
            Marshal.Copy(struParam.pOutBuffer, managedArray, 0, (int)iXMSize);
            string strOutBuffer = Encoding.UTF8.GetString(managedArray);
            if (strOutBuffer.Contains("multipart/form-data"))//表单格式的话，通过分隔符截取
            {
                int nullIndex = strOutBuffer.IndexOf("--MIME_boundary--");
                if (nullIndex != -1)
                {
                    strOutBuffer = strOutBuffer.Substring(0, nullIndex);
                }
            }
            else
            {//非表单格式，通过'\0'截取
                int nullIndex = strOutBuffer.IndexOf('\0');
                if (nullIndex != -1)
                {
                    strOutBuffer = strOutBuffer.Substring(0, nullIndex);    //截取字符串，去掉后面乱码部分
                }
            }
            Console.WriteLine("OTAP_CMS_ISAPIPassThrough succ");
            if (strOutBuffer != "")
            {
                Console.WriteLine("strOutBuffer:" + strOutBuffer);
            }
            Marshal.FreeHGlobal(struParam.pRequestUrl);
            if (inputStr != "")
            {
                Marshal.FreeHGlobal(struParam.pInBuffer);
            }
            Marshal.FreeHGlobal(struParam.pOutBuffer);
        }

        //下行操作 平台——>设备
        public static void Cms_SetConfigDev(HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM enumMsg, string sDomain, string sIdentifier, string inputData)
        {
            HCOTAPCMS.OTAP_CMS_CONFIG_DEV_PARAM struConfigParam = new();
            struConfigParam.Init();
            //子设备ID,设备本身固定为global
            string sChildID = "global";
            sChildID.CopyTo(0, struConfigParam.szChildID, 0, sChildID.Length);
            //设备本地资源标识,设备本身固定为0
            string sLocalIndex = "0";
            sLocalIndex.CopyTo(0, struConfigParam.szLocalIndex, 0, sLocalIndex.Length);
            //设备资源类型,设备本身固定为global
            string sResourceType = "global";
            sResourceType.CopyTo(0, struConfigParam.szResourceType, 0, sResourceType.Length);
            //功能领域，不同功能对应不同领域，详见OTAP协议文档
            sDomain.CopyTo(0, struConfigParam.szDomain, 0, sDomain.Length);
            //功能标识/属性标识，不同功能对应不同领域，详见OTAP协议文档
            sIdentifier.CopyTo(0, struConfigParam.szIdentifier, 0, sIdentifier.Length);

            //输入参数
            if (inputData != "")
            {
                byte[] byInputParam = Encoding.UTF8.GetBytes(inputData);
                int iXMLInputLen = byInputParam.Length;
                struConfigParam.pInBuf = Marshal.AllocHGlobal(iXMLInputLen);
                Marshal.Copy(byInputParam, 0, struConfigParam.pInBuf, iXMLInputLen);
                struConfigParam.dwInBufSize = (uint)byInputParam.Length;
                Console.WriteLine("下行操作输入报文:" + inputData);
            }

            struConfigParam.pOutBuf = Marshal.AllocHGlobal(20 * 1024);    //输出缓冲区，如果接口调用失败提示错误码43，需要增大输出缓冲区
            struConfigParam.dwOutBufSize = 20 * 1024;

            if (!HCOTAPCMS.OTAP_CMS_ConfigDev(OtapTest.lLoginID, enumMsg, ref struConfigParam))
            {
                Console.WriteLine("OTAP_CMS_ConfigDev failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                return;
            }

            uint iXMSize = struConfigParam.dwOutBufSize;
            byte[] managedArray = new byte[iXMSize];
            Marshal.Copy(struConfigParam.pOutBuf, managedArray, 0, (int)iXMSize);
            string strOutBuffer = Encoding.UTF8.GetString(managedArray).TrimEnd('\0');
            Console.WriteLine("OTAP_CMS_ConfigDev succ");
            int nullIndex = strOutBuffer.IndexOf('\0');
            if (nullIndex != -1)
            {
                strOutBuffer = strOutBuffer.Substring(0, nullIndex);    //截取字符串，去掉后面乱码部分
            }

            if (strOutBuffer != "")
            {
                Console.WriteLine("strOutBuffer:" + strOutBuffer);
            }
            if (inputData != "")
            {
                Marshal.FreeHGlobal(struConfigParam.pInBuf);
            }
            Marshal.ZeroFreeGlobalAllocUnicode(struConfigParam.pOutBuf);
        }
    }
}