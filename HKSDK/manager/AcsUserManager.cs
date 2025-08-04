using GrpcService.Common;
using GrpcService.HKSDK.service;

namespace GrpcService.HKSDK.manager
{
    class AcsUserManagement
    {
        static string inputUrl = "";
        static string inputBuffer = "";
        static Dictionary<string, object>? parameter;

        /*
         * 添加人员信息
         */
        public static void AddEmployeeInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
            parameter = new Dictionary<string, object>
            {
                { "employeeNo", "employeeNo1" },     //工号
                { "name", "测试姓名1" }     //姓名
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AddUserInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 配置人员信息及权限删除
         */
        public static void DeleteEmployeeInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json";
            parameter = new Dictionary<string, object>
            {
                { "mode", "byEmployeeNo" },  //删除模式, [all#删除所有,byEmployeeNo#按工号,byUserType#按用户类型]
                { "employeeNo", "employeeNo1" }     //工号
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\DeleteUserInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 查询人员信息
         */
        public static void SearchEmployeeInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "POST /ISAPI/AccessControl/UserInfo/Search?format=json";
            parameter = [];
            Guid uuid = Guid.NewGuid();
            parameter.Add("searchID", uuid);    // 查询id, 更换查询条件时，保证不重复即可
            parameter.Add("maxResults", "30");  //最大查询数量
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\SearchUserInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }
    }

    class AcsCardManagement
    {
        static string inputUrl = "";
        static string inputBuffer = "";
        static Dictionary<string, object> parameter;

        /*
         * 添加卡信息
         */
        public static void AddCardInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json";
            parameter = new Dictionary<string, object>
            {
                { "employeeNo", "employeeNo1" },    //人员工号信息
                { "cardNo", "1234567890" }  //卡号信息
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AddCardInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 删除卡信息
         */
        public static void DeleteCardInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/AccessControl/CardInfo/Delete?format=json";
            parameter = new Dictionary<string, object>
            {
                { "cardNo", "1234567890" }    //卡号信息
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\DeleteCardInfo.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 查询卡信息
         */
        public static void SearchCardInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "POST /ISAPI/AccessControl/CardInfo/Search?format=json";
            parameter = [];
            Guid uuid = Guid.NewGuid();
            parameter.Add("searchID", uuid);    // 查询id, 更换查询条件时，保证不重复即可
            parameter.Add("employeeNo", "employeeNo1");  //人员工号信息
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\SearchCardInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }
    }

    class AcsFaceManagement
    {
        static string inputUrl = "";
        static string inputBuffer = "";
        static Dictionary<string, object>? parameter;

        /*
         * 设置人脸数据
         */
        public static void AddFacePicInfo(int loginID, string faceURL)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/Intelligent/FDLib/FDSetUp?format=json";
            parameter = new Dictionary<string, object>
            {
                { "employeeNo", "employeeNo1" },     //工号
                { "faceURL", faceURL }     //图片需要使用URL方式下发
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\AddFaceInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 删除人脸库中的人脸数据
         */
        public static void DeleteFacePicInfo(int loginID)
        {
            inputUrl = "";
            inputUrl = "PUT /ISAPI/Intelligent/FDLib/FDSearch/Delete?format=json&FDID=1&faceLibType=blackFD";
            parameter = new Dictionary<string, object>
            {
                { "employeeNo", "employeeNo1" }     //工号
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\DeleteFaceInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }

        /*
         * 查找人脸库中的人脸数据
         */
        public static void SearchFacePicInfo(int loginID)   //注意，返回的是form-data格式的报文+图片二进制数据，需要根据分隔符来解析图片二进制数据
        {
            inputUrl = "";
            inputUrl = "POST /ISAPI/Intelligent/FDLib/FDSearch?format=json";
            parameter = new Dictionary<string, object>
            {
                { "employeeNo", "employeeNo1" }     //工号
            };
            inputBuffer = ConfigFileUtil.GetReqBodyFromTemplate("\\conf\\Acs\\SearchFaceInfoParam.json", parameter);
            CMSServiceHelpers.Cms_ISAPIPassThrough(loginID, inputUrl, inputBuffer);
        }
    }
}
