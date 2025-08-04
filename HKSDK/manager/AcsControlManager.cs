using GrpcService.Common;
using GrpcService.HKSDK.service;

namespace GrpcService.HKSDK.manager
{
    public class AcsControlManager
    {
        static string inputUrl = "";
        static string inputBuffer = "";
        static Dictionary<string, object>? parameter;

        /*
         * 获取门禁主机参数
         */
        public static void GetAcsCfg(int loginID)
        {
            inputUrl = "";
            inputUrl = "GET /ISAPI/AccessControl/AcsCfg?format=json";
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, "");
        }

        /*
         * 设置门禁主机参数
         */
        public static void SetAcsCfg(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/AcsCfg?format=json";
            parameter = new Dictionary<string, object>
            {
                { "uploadCapPic", "true" }     //设置抓拍图片上传
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AcsCfgParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 获取门禁主机工作状态
         */
        public static void GetAcsStatus(int loginID)
        {
            inputUrl = "";
            inputUrl = "GET /ISAPI/AccessControl/AcsWorkStatus?format=json";
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, "");
        }

        /*
         * 远程控门
         */
        public static void RemoteControlDoor(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/RemoteControl/door/1";
            parameter = new Dictionary<string, object>
            {
                { "cmd", "open" }     //远程开门参数
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AcsRemoteControlDoor.xml", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 获取人员通道状态
         */
        public static void GetGateStatus(int loginID)
        {
            inputUrl = "";
            inputUrl = "GET /ISAPI/AccessControl/GateStatus";
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, "");
        }

    }
}
