using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace GrpcService.Models
{
    /// <summary>
    /// 海康那个sdk设备信息返回
    /// </summary>
    [XmlRoot("DeviceInfo", Namespace = "http://www.isapi.org/ver20/XMLSchema")]
    public class GrpcDeviceInfo
    {
        [XmlElement("deviceName")]
        public string? DeviceName { get; set; }
        [XmlElement("deviceID")]
        public string? DeviceID { get; set; }
        [XmlElement("model")]
        public string? Model { get; set; }
        [XmlElement("serialNumber")]
        public string? SerialNumber { get; set; }
        [XmlElement("macAddress")]
        public string? MacAddress { get; set; }
        [XmlElement("firmwareVersion")]
        public string? FirmwareVersion { get; set; }
        [XmlElement("firmwareReleasedDate")]
        public string? FirmwareReleasedDate { get; set; }
        [XmlElement("encoderVersion")]
        public string? EncoderVersion { get; set; }
        [XmlElement("encoderReleasedDate")]
        public string? EncoderReleasedDate { get; set; }
        [XmlElement("hardwareVersion")]
        public string? HardwareVersion { get; set; }
        [XmlElement("deviceType")]
        public string? DeviceType { get; set; }
        [XmlElement("subDeviceType")]
        public string? SubDeviceType { get; set; }
        [XmlElement("localZoneNum")]
        public string? LocalZoneNum { get; set; }
        [XmlElement("alarmOutNum")]
        public string? AlarmOutNum { get; set; }
        [XmlElement("relayNum")]
        public string? RelayNum { get; set; }
        [XmlElement("electroLockNum")]
        public string? ElectroLockNum { get; set; }
        public string? RS485Num { get; set; }
        [XmlElement("manufacturer")]
        public string? Manufacturer { get; set; }
        public string? OEMCode { get; set; }
        [XmlElement("marketType")]
        public string? MarketType { get; set; }
        [XmlElement("dispalyNum")]
        public string? DispalyNum { get; set; }
        [XmlElement("bspVersion")]
        public string? BspVersion { get; set; }
        [XmlElement("dspVersion")]
        public string? DspVersion { get; set; }
        [XmlElement("productionDate")]
        public string? ProductionDate { get; set; }
    }
    /// <summary>
    /// 海康sdk获取用户数量返回
    /// </summary>
    public class GrpcUserInfo
    {
        public UserInfoCount? UserInfoCount { get; set; } = new UserInfoCount();
    }

    public class UserInfoCount
    {
        [JsonPropertyName("userNumber")]
        public int UserNumber { get; set; }
        [JsonPropertyName("bindFaceUserNumber")]
        public int BindFaceUserNumber { get; set; }
        [JsonPropertyName("bindFingerprintUserNumber")]
        public int BindFingerprintUserNumber { get; set; }
        [JsonPropertyName("bindCardUserNumber")]
        public int BindCardUserNumber { get; set; }
    }

    /// <summary>
    /// 海康sdk查询用户请求
    /// </summary>
    public class UserInfoSearchCond
    {
        /// <summary>
        /// 【必需】搜索记录唯一标识
        /// 说明：用来确认上层客户端是否为同一个(倘若是同一个，设备记录内存，下次搜索加快速度)
        /// 类型：string
        /// </summary>
        public required string SearchID { get; set; }

        /// <summary>
        /// 【必需】查询结果起始位置
        /// 说明：
        /// 1. 当记录条数很多时，一次查询不能获取所有记录，指定此位置可查询后续记录
        ///    （若设备支持的最大totalMatches为M个，当前存储N个（N≤M），则合法范围0~N-1）
        /// 2. 当sortByNameFlag下发时，表示按姓名排序后人员的位置编号（10万人员对应0-99999）
        /// 3. UI首次进入通讯录页面时，此值应为0，sortByNameFlag为#
        /// 4. 当sortByBoxNumFlag为true时，表示按占用格口数量排序后的位置编号
        /// 类型：int
        /// </summary>
        public required int SearchResultPosition { get; set; }

        /// <summary>
        /// 【必需】最大返回记录数
        /// 说明：
        /// - 如值大于设备能力集范围，则按能力集最大值返回，不报错
        /// 类型：int
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// 【可选】人员ID列表
        /// 说明：
        /// - 当字段不存在或为空时，查询所有用户
        /// 子类型：object数组
        /// </summary>
        public List<EmployeeNoList>? EmployeeNoList { get; set; }

        /// <summary>
        /// 【可选】模糊查询关键字
        /// 约束：
        /// - 长度范围：[1,32]
        /// - 敏感信息需加密
        /// 类型：string
        /// </summary>
        public string? FuzzySearch { get; set; }

        /// <summary>
        /// 【可选】考勤组织编号范围
        /// 子类型：int数组
        /// </summary>
        public List<int>? GroupIdList { get; set; }

        /// <summary>
        /// 【可选】考勤组织名称范围
        /// 子类型：string数组
        /// </summary>
        public List<string>? GroupNameList { get; set; }

        /// <summary>
        /// 【可选】排班类型
        /// 枚举值：
        /// - personal#个人排班
        /// - group#部门排班 
        /// - all#所有已排班
        /// - none#所有未排班
        /// 说明：存在时表示查询对应排班状态的人
        /// 子类型：string
        /// </summary>
        public string? ArrangeType { get; set; }

        /// <summary>
        /// 【可选】人员类型
        /// 枚举值：
        /// - normal#普通人（主人）
        /// - visitor#来宾（访客）
        /// - blackList#非授权名单人
        /// - patient#患者
        /// - maintenance#维护人员（任意时间都能进入房间）
        /// - custom1#自定义类型1
        /// - ...（custom2-custom5）
        /// 子类型：string
        /// </summary>
        public string? UserType { get; set; }

        /// <summary>
        /// 【可选】设备编号列表
        /// 说明：要查询的设备编号(床位)列表
        /// 子类型：int数组
        /// </summary>
        public List<int>? DeviceIDList { get; set; }

        /* 以下为生物特征标识字段（全部可选）*/

        /// <summary>
        /// 是否已录入人脸
        /// 类型：bool
        /// </summary>
        public bool? HasFace { get; set; }

        /// <summary>
        /// 是否已录入卡
        /// 类型：bool
        /// </summary>
        public bool? HasCard { get; set; }

        /// <summary>
        /// 是否已录入指纹
        /// 类型：bool
        /// </summary>
        public bool? HasFingerprint { get; set; }

        /// <summary>
        /// 是否已录入虹膜
        /// 类型：bool
        /// </summary>
        public bool? HasIris { get; set; }

        /// <summary>
        /// 是否已录入声纹
        /// 类型：bool
        /// </summary>
        public bool? HasVoiceprint { get; set; }

        /// <summary>
        /// 是否已录入身份证
        /// 类型：bool
        /// </summary>
        public bool? HasIDCard { get; set; }

        /// <summary>
        /// 是否已完成凭证采集
        /// 说明：当设备处于采集模式时，用于区分采集名单中人员是否已完成采集
        /// 类型：bool
        /// </summary>
        public bool? HasCollection { get; set; }

        /// <summary>
        /// 是否已录入遥控器
        /// 类型：bool
        /// </summary>
        public bool? HasRemoteControl { get; set; }

        /// <summary>
        /// 是否已录入掌纹掌静脉
        /// 类型：bool
        /// </summary>
        public bool? HasPPAndPV { get; set; }

        /// <summary>
        /// 【可选】用户级别
        /// 枚举值：
        /// - Employee#普通员工
        /// - DepartmentManager#部门主管
        /// 子类型：string
        /// </summary>
        public string? UserLevel { get; set; }

        /// <summary>
        /// 【可选】会议编号
        /// 说明：可根据会议ID查询该会议下的人员信息
        /// 约束：
        /// - UUID格式
        /// - 长度范围：[1,32]
        /// 类型：string
        /// </summary>
        public string? MeetingID { get; set; }

        /// <summary>
        /// 【可选】姓名排序标识
        /// 枚举值：
        /// - !#姓名特殊人员（空或首字符为特殊符号）
        /// - A#A
        /// - ...（B-Z）
        /// 说明：
        /// 1. 用于首次进入通讯录页面（!）和按类型查询
        /// 2. !在设备UI显示为#类别
        /// 3. 若查询数量大于实际数据量，自动补充下一字母数据
        /// 子类型：string
        /// </summary>
        public string? SortByNameFlag { get; set; }

        /// <summary>
        /// 【可选】格口占用数量排序标识
        /// 说明：
        /// 1. 默认false
        /// 2. 为true时按人员占用格口数量从大到小排序
        ///    数量相同时按添加到设备的顺序排序（先添加的靠前）
        /// 类型：bool
        /// </summary>
        public bool? SortByBoxNumFlag { get; set; }

        /// <summary>
        /// 【可选】是否关联人员组织
        /// 说明：
        /// - true: 查询关联人员组织的人员（需配合userGroupNodeID字段）
        /// - false: 查询未关联人员组织的人员
        /// 类型：bool
        /// </summary>
        public bool? IsLinkageUserGroup { get; set; }

        /// <summary>
        /// 【可选】人员组织编号
        /// 约束：
        /// - 长度范围：[1,32]
        /// - 由数字、大小写字母组成
        /// 依赖条件：当IsLinkageUserGroup为true时必须提供
        /// 类型：string
        /// </summary>
        public string? UserGroupNodeID { get; set; }

        /// <summary>
        /// 【可选】是否显示子人员组织
        /// 说明：
        /// - 默认值：true
        /// 类型：bool
        /// </summary>
        public bool? IsDisplaySubGroupNode { get; set; }

        /// <summary>
        /// 【可选】群组编号
        /// 说明：指多重认证中的群组概念
        /// 约束：
        /// - 范围：[1,32]
        /// 类型：int
        /// </summary>
        public int? GroupID { get; set; }

        /// <summary>
        /// 【可选】遥控器号
        /// 约束：
        /// - 长度范围：[1,9]
        /// 类型：string
        /// </summary>
        public string? RemoteControlNo { get; set; }

        /// <summary>
        /// 【可选】区域权限组关联标识
        /// 类型：bool
        /// </summary>
        public bool? RegionPermissionGroupFlag { get; set; }

        /// <summary>
        /// 【可选】区域权限组编号
        /// 约束：
        /// - 范围：[1,32]
        /// 配合规则：
        /// 1. 必须与regionPermissionGroupFlag配合使用
        /// 2. 当regionPermissionGroupFlag为true时：查询该组已关联人员
        /// 3. 当regionPermissionGroupFlag为false时：查询未关联该组的人员
        /// 类型：int
        /// </summary>
        public int? RegionPermissionGroupID { get; set; }

        /// <summary>
        /// 【可选】区域权限组分配人员类型
        /// 当前枚举值：
        /// - allFailure#历史分配失败的全部人员
        /// 配合规则：
        /// - 必须与regionPermissionGroupFlag和regionPermissionGroupID配合使用
        /// 子类型：string
        /// </summary>
        public string? GroupDistributeUserType { get; set; }

        /// <summary>
        /// 【可选】房间号
        /// 类型：int
        /// </summary>
        public int? RoomNumber { get; set; }
    }

    public class EmployeeNoList
    {
        /// <summary>
        /// 【必需】工号
        /// 类型：string
        /// </summary>
        public required string EmployeeNo { get; set; }
    }
    /// <summary>
    /// 海康sdk查询人员返回
    /// </summary>
    public class SdkWhiteResponse
    {
        public required UserInfoSearch UserInfoSearch { get; set; }
    }
    public class UserInfoSearch
    {
        /// <summary>
        /// 【只读|必需】搜索记录唯一标识
        /// 类型：string
        /// </summary>
        public required string SearchID { get; set; }

        /// <summary>
        /// 【只读|必需】查询状态字符串描述
        /// 枚举值：
        /// - OK#查询结束
        /// - MORE#还有数据等待查询
        /// - NO MATCH#没有匹配数据
        /// 类型：string
        /// </summary>
        public required string ResponseStatusStrg { get; set; }

        /// <summary>
        /// 【只读|必需】本次返回的记录条数
        /// 类型：int
        /// </summary>
        public required int NumOfMatches { get; set; }

        /// <summary>
        /// 【只读|必需】符合条件的记录总条数
        /// 类型：int
        /// </summary>
        public required int TotalMatches { get; set; }

        /// <summary>
        /// 【只读|可选】人员信息列表
        /// 子类型：UserInfo对象数组
        /// </summary>
        public List<UserInfo>? UserInfo { get; set; }
    }
    public class UserInfo
    {
        /* ==================== 基础信息 ==================== */
        /// <summary>
        /// 【必需】工号（人员ID）
        /// 类型：string
        /// 约束：非空唯一标识
        /// </summary>
        public required string EmployeeNo { get; set; }

        /// <summary>
        /// 【可选】是否删除该人员
        /// 类型：bool
        /// 说明：true表示执行删除操作
        /// </summary>
        public bool? DeleteUser { get; set; }

        /// <summary>
        /// 【可选】姓名
        /// 类型：string
        /// 长度限制：≤64字符
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 【必需】人员类型
        /// 枚举值：
        /// - normal#普通人（主人）
        /// - visitor#来宾（访客）
        /// - blackList#非授权名单人
        /// - maintenance#维护人员（包括保洁、维修等）
        /// - patient#病患
        /// - custom1#自定义类型1
        /// - custom2#自定义类型2
        /// - custom3#自定义类型3
        /// - custom4#自定义类型4
        /// - custom5#自定义类型5
        /// 依赖条件：DeleteUser为false或不存在时必需
        /// 特殊说明：维护人员可随时进入受控区域
        /// </summary>
        public required string UserType { get; set; }

        /// <summary>
        /// 【可选】占用格口数量
        /// 类型：int
        /// 范围：0-200
        /// 说明：0表示未占用
        /// </summary>
        public int? BoxNum { get; set; }

        /* ==================== 权限控制 ==================== */
        /// <summary>
        /// 【可选】是否仅认证
        /// 类型：bool
        /// 说明：true表示仅作身份认证，无其他权限
        /// </summary>
        public bool? OnlyVerify { get; set; }

        /// <summary>
        /// 【可选】门权限（主权限）
        /// 类型：string
        /// 格式：逗号分隔的门编号（如"1,3"）
        /// 说明：与RightPlan配合使用，为空表示无权限
        /// </summary>
        public string? DoorRight { get; set; }

        /// <summary>
        /// 【可选】门权限计划（时段权限）
        /// 子类型：RightPlan数组
        /// 约束：
        /// 1. 必须与DoorRight同时存在
        /// 2. 计划中的门必须是DoorRight的子集
        /// </summary>
        public List<RightPlan>? RightPlan { get; set; }

        /// <summary>
        /// 【可选】反锁开门权限
        /// 类型：bool
        /// 说明：true-可解除门锁反锁状态
        /// </summary>
        public bool? DoubleLockRight { get; set; }

        /// <summary>
        /// 【可选】本地UI访问权限
        /// 类型：bool
        /// 说明：true-可操作设备本地界面
        /// </summary>
        public bool? LocalUIRight { get; set; }

        /// <summary>
        /// 【可选】关联用户序号
        /// 枚举值：
        /// - 1#本地管理员（基础管理权限）
        /// - 2#人员管理员（增删改查权限）
        /// 依赖条件：LocalUIRight为true时有效
        /// </summary>
        public int? LinkageUserID { get; set; }

        /* ==================== 时间控制 ==================== */
        /// <summary>
        /// 【条件必需】有效期参数
        /// 依赖条件：DeleteUser为false或不存在时必需
        /// 说明：
        /// - enable=false表示永久有效
        /// - 时间范围：1970-01-01至2037-12-31
        /// </summary>
        public ValidInfo? Valid { get; set; }

        /// <summary>
        /// 【可选】是否关门延迟
        /// 类型：bool
        /// 说明：true-开门后延迟关闭
        /// </summary>
        public bool? CloseDelayEnabled { get; set; }

        /// <summary>
        /// 【可选】最大认证次数
        /// 类型：int
        /// 说明：0表示不限制
        /// </summary>
        public int? MaxOpenDoorTime { get; set; }

        /// <summary>
        /// 【可选】已认证次数
        /// 类型：int
        /// 说明：只读统计值
        /// </summary>
        public int? OpenDoorTime { get; set; }

        /* ==================== 空间信息 ==================== */
        /// <summary>
        /// 【可选】房间号
        /// 类型：int
        /// 说明：传统房间编号
        /// </summary>
        public int? RoomNumber { get; set; }

        /// <summary>
        /// 【可选】层号
        /// 类型：int
        /// 说明：传统楼层编号
        /// </summary>
        public int? FloorNumber { get; set; }

        /// <summary>
        /// 【可选】呼叫号码列表（新标准）
        /// 子类型：string数组
        /// 编码规则：
        /// 楼宇对讲（国内）：1-1-1-层号(3位)+房号(2位)
        /// 医疗对讲：1-1-1-房间号-设备编号
        /// 优先级高于RoomNumber
        /// </summary>
        public List<string>? CallNumbers { get; set; }

        /// <summary>
        /// 【可选】层号列表（新标准）
        /// 子类型：int数组
        /// 约束：与CallNumbers元素一一对应
        /// </summary>
        public List<int>? FloorNumbers { get; set; }

        /* ==================== 组织架构 ==================== */
        /// <summary>
        /// 【可选】所属群组
        /// 类型：string
        /// 格式：逗号分隔的群组ID（如"1,3,5"）
        /// </summary>
        public string? BelongGroup { get; set; }

        /// <summary>
        /// 【可选】关联更新认证人数
        /// 类型：bool
        /// 说明：
        /// true-自动更新多重认证组的参与人数
        /// false-默认值
        /// </summary>
        public bool? LinkageUpdateAuthMemberNum { get; set; }

        /// <summary>
        /// 【可选】人员组织编号
        /// 类型：string
        /// 范围：1-32字符
        /// 格式：数字/字母组成
        /// </summary>
        public string? UserGroupNodeID { get; set; }

        /// <summary>
        /// 【可选】组织架构名称
        /// 类型：string
        /// 长度：1-64字符
        /// </summary>
        public string? GroupName { get; set; }

        /* ==================== 认证凭证 ==================== */
        /// <summary>
        /// 【可选】密码
        /// 类型：string
        /// 范围：0-8字符
        /// 说明：空表示未设置
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// 【可选】单机密码
        /// 类型：string
        /// 范围：4-8字符
        /// 说明：仅单机管理模式有效
        /// </summary>
        public string? LocalPassword { get; set; }

        /// <summary>
        /// 【可选】登录账号
        /// 类型：string
        /// 范围：1-32字符
        /// 约束：需与LoginPassword同时设置
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// 【可选】登录密码
        /// 类型：string
        /// 范围：8-16字符
        /// 复杂度：需包含两种及以上字符类型
        /// </summary>
        public string? LoginPassword { get; set; }

        /// <summary>
        /// 【可选】动态权限码
        /// 类型：string
        /// 说明：临时访问凭证
        /// </summary>
        public string? DynamicCode { get; set; }

        /* ==================== 生物特征 ==================== */
        /// <summary>
        /// 【可选】关联人脸数量
        /// 类型：int
        /// 说明：null表示未录入
        /// </summary>
        public int? NumOfFace { get; set; }

        /// <summary>
        /// 【可选】关联指纹数量
        /// 类型：int
        /// </summary>
        public int? NumOfFP { get; set; }

        /// <summary>
        /// 【可选】关联卡数量
        /// 类型：int
        /// </summary>
        public int? NumOfCard { get; set; }

        /// <summary>
        /// 【可选】关联虹膜数量
        /// 类型：int
        /// </summary>
        public int? NumOfIris { get; set; }

        /// <summary>
        /// 【可选】关联掌纹掌静脉数
        /// 类型：int
        /// </summary>
        public int? NumOfPPAndPV { get; set; }

        /// <summary>
        /// 【可选】关联身份证数量
        /// 类型：int
        /// 说明：HEOP专用字段
        /// </summary>
        public int? NumOfIDCard { get; set; }

        /* ==================== 人员特征 ==================== */
        /// <summary>
        /// 【可选】性别
        /// 枚举值：
        /// - male#男
        /// - female#女
        /// - unknown#未知
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>
        /// 【可选】年龄
        /// 类型：int
        /// 范围：0-120
        /// </summary>
        public int? Age { get; set; }

        /// <summary>
        /// 【可选】手机号码
        /// 类型：string
        /// 长度：1-32字符
        /// </summary>
        public string? PhoneNo { get; set; }

        /* ==================== 扩展信息 ==================== */
        /// <summary>
        /// 【可选】人员信息扩展
        /// 子类型：PersonInfoExtend数组
        /// 用途：设备UI个性化显示
        /// </summary>
        public List<PersonInfoExtend>? PersonInfoExtends { get; set; }

        /// <summary>
        /// 【可选】预置图片编号
        /// 类型：string
        /// 范围：1-32字符
        /// 说明：替代真实人像的卡通图片ID
        /// </summary>
        public string? PicID { get; set; }

        /// <summary>
        /// 【可选】人脸图片URL
        /// 类型：string
        /// 格式：
        /// - 本地设备：file://路径
        /// - 网络存储：http(s)://URL
        /// </summary>
        public string? FaceURL { get; set; }

        /// <summary>
        /// 【可选】人脸抠图URL
        /// 类型：string
        /// 说明：背景透明的人脸特写
        /// </summary>
        public string? FaceMattingURL { get; set; }

        /* ==================== 特殊功能 ==================== */
        /// <summary>
        /// 【可选】ESD检测类型
        /// 枚举值：
        /// - handAndFoot#手脚同检
        /// - no#不检测（默认）
        /// - hand#仅手部
        /// - foot#仅脚部
        /// </summary>
        public string? ESDType { get; set; }

        /// <summary>
        /// 【可选】用户级别
        /// 枚举值：
        /// - Employee#普通员工
        /// - DepartmentManager#部门主管
        /// </summary>
        public string? UserLevel { get; set; }

        /// <summary>
        /// 【可选】是否重复校验
        /// 类型：bool
        /// 说明：
        /// - true#校验人员是否已存在（默认）
        /// - false#跳过校验（批量导入时提速）
        /// </summary>
        public bool? CheckUser { get; set; }

        /* ==================== 子系统集成 ==================== */
        /// <summary>
        /// 【可选】考勤组织编号
        /// 类型：int
        /// 说明：关联考勤子系统
        /// </summary>
        public int? GroupId { get; set; }

        /// <summary>
        /// 【可选】考勤计划模板
        /// 类型：int
        /// </summary>
        public int? LocalAtndPlanTemplateId { get; set; }

        /// <summary>
        /// 【可选】会议签到状态
        /// 枚举值：
        /// - normal#正常签到
        /// - late#迟到
        /// - notSignIn#未签到
        /// </summary>
        public string? MeetingSignInStatus { get; set; }

        /* ==================== 设备信息 ==================== */
        /// <summary>
        /// 【可选】设备编号
        /// 类型：string
        /// 说明：绑定的设备唯一标识
        /// </summary>
        public string? DeviceID { get; set; }

        /// <summary>
        /// 【可选】所属设备列表
        /// 子类型：DeviceInfo数组
        /// 用途：多设备分配（如访客指定门口机）
        /// </summary>
        public List<DeviceInfo>? DeviceInfoList { get; set; }

        /* ==================== 状态标记 ==================== */
        /// <summary>
        /// 【可选】是否上传采集平台
        /// 类型：bool
        /// 说明：HEOP专用标记
        /// </summary>
        public bool? UploadedToTheCollectionPlatform { get; set; }

        /// <summary>
        /// 【可选】是否在采集名单
        /// 类型：bool
        /// 说明：HEOP采集模式专用
        /// </summary>
        public bool? CollectionUserList { get; set; }

        /* ==================== 排序标识 ==================== */
        /// <summary>
        /// 【可选】姓名排序位置
        /// 类型：int
        /// 范围：0-99999
        /// 说明：按姓名排序后的索引号
        /// </summary>
        public int? SortByNamePosition { get; set; }

        /// <summary>
        /// 【可选】姓名首字母标识
        /// 枚举值：
        /// - !#特殊字符开头
        /// - A-Z#字母开头
        /// </summary>
        public string? SortByNameFlag { get; set; }
    }

    public class ValidInfo
    {
        /// <summary>
        /// 【只读|必需】使能有效期
        /// 类型：bool
        /// </summary>
        public required bool Enable { get; set; }

        /// <summary>
        /// 【只读|必需】有效期起始时间
        /// 说明：timeType字段不存在或为local时
        /// 类型：string
        /// </summary>
        public required string BeginTime { get; set; }

        /// <summary>
        /// 【只读|必需】有效期结束时间
        /// 说明：timeType字段不存在或为local时
        /// 类型：string
        /// </summary>
        public required string EndTime { get; set; }

        /// <summary>
        /// 【只读|可选】时间类型
        /// 类型：string
        /// </summary>
        public string? TimeType { get; set; }
    }
    public class RightPlan
    {
        /// <summary>
        /// 【只读|可选】门编号（锁ID）
        /// 类型：int
        /// </summary>
        public int? DoorNo { get; set; }

        /// <summary>
        /// 【只读|可选】门名称
        /// 范围：[0,32]
        /// 类型：string
        /// </summary>
        public string? DoorName { get; set; }

        /// <summary>
        /// 【只读|可选】所属区域名称
        /// 范围：[0,32]
        /// 类型：string
        /// </summary>
        public string? RegionNodeName { get; set; }

        /// <summary>
        /// 【只读|可选】计划模板编号
        /// 说明：
        /// - 同个门不同计划模板采用权限"或"的方式处理
        /// - 默认无计划模板编号
        /// - 65535-7*24小时生效
        /// - 65534-周一到周五24小时生效
        /// - 65533-周六周日24小时生效
        /// 类型：string
        /// </summary>
        public string? PlanTemplateNo { get; set; }

        /// <summary>
        /// 【只读|可选】计划模板名称
        /// 说明：计划模板名称和计划模板编号一一对应
        /// 子类型：string数组
        /// </summary>
        public List<string>? PlanTemplateName { get; set; }
    }
    public class PersonInfoExtend
    {
        /// <summary>
        /// 【只读|可选】人员信息扩展序号
        /// 范围：[1,32]
        /// 说明：与/ISAPI/AccessControl/personInfoExtendName?format=json中的id对应
        /// 类型：int
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// 【只读|可选】人员信息扩展内容
        /// 类型：string
        /// </summary>
        public string? Value { get; set; }
    }
    public class PatientInfo
    {
        /// <summary>
        /// 【只读|可选】设备编号（对应编号配置中编号）
        /// 类型：string
        /// </summary>
        public string? DeviceID { get; set; }

        /// <summary>
        /// 【只读|可选】入院时间
        /// 类型：DateTime
        /// </summary>
        public DateTime? AdmissionTime { get; set; }

        /// <summary>
        /// 【只读|可选】责任护士
        /// 范围：[0,32]
        /// 类型：string
        /// </summary>
        public string? ChargeNurse { get; set; }

        /// <summary>
        /// 【只读|可选】责任医师
        /// 范围：[0,32]
        /// 类型：string
        /// </summary>
        public string? ChargeDoctor { get; set; }

        /// <summary>
        /// 【只读|可选】护理等级
        /// 枚举值：
        /// - unknow#未知
        /// - tertiary#三级护理
        /// - secondary#二级护理
        /// - primary#一级护理
        /// - special#特级护理
        /// 类型：string
        /// </summary>
        public string? NursingLevel { get; set; }

        /// <summary>
        /// 【只读|可选】医嘱注意事项
        /// 范围：[0,128]
        /// 类型：string
        /// </summary>
        public string? DoctorsAdvice { get; set; }

        /// <summary>
        /// 【只读|可选】过敏情况
        /// 范围：[0,128]
        /// 类型：string
        /// </summary>
        public string? AllergicHistory { get; set; }
    }
    public class TromboneRule
    {
        /// <summary>
        /// 【只读|可选】行业(场景)类型
        /// 枚举值：
        /// - builidings#楼宇
        /// - prison#监所
        /// - medicalTreatment#医疗
        /// - broadcasting#广播
        /// 说明：人员关联设备的行业类型
        /// 类型：string
        /// </summary>
        public string? IndustryType { get; set; }

        /// <summary>
        /// 【只读|可选】设备类型
        /// 枚举值：
        /// - indoor#室内机
        /// - villa#别墅门口机
        /// - confirm#二次确认机
        /// - outdoor#门口机
        /// - fence#围墙机
        /// - doorbell#门铃机
        /// - manage#管理机
        /// - acs#门禁设备
        /// - wardStation#探视分机
        /// - bedheadExtension#床头分机
        /// - bedsideExtension#床旁分机
        /// - terminal#终端
        /// - netAudio#网络音响
        /// - interactive#交互终端
        /// - amplifier#功放设备
        /// 说明：人员关联设备的设备类型
        /// 类型：string
        /// </summary>
        public string? UnitType { get; set; }

        /// <summary>
        /// 【只读|可选】SIP(指私有SIP)版本
        /// 范围：[0,32]
        /// 说明：人员关联设备的私有SIP版本号
        /// 类型：string
        /// </summary>
        public string? SIPVersion { get; set; }
    }
    public class LocalAttendance
    {
        /// <summary>
        /// 【只读|可选】模板/排班名称
        /// 说明：该人员当前所属的排班名称
        /// 类型：string
        /// </summary>
        public string? TemplateName { get; set; }
    }
    public class DeviceInfo
    {
        /// <summary>
        /// 【只读|必需】设备短序列号
        /// 范围：[9,9]
        /// 类型：string
        /// </summary>
        public required string DevShortSerialNum { get; set; }

        /// <summary>
        /// 【只读|可选】设备名称
        /// 范围：[0,64]
        /// 类型：string
        /// </summary>
        public string? DevName { get; set; }

        /// <summary>
        /// 【只读|可选】人员已认证次数
        /// 范围：[0,1000]
        /// 类型：int
        /// </summary>
        public int? OpenDoorTime { get; set; }
    }

    /// <summary>
    /// 卡查询条件
    /// </summary>
    public class CardInfoSearchCond
    {
        /// <summary>
        /// 【必需】搜索记录唯一标识
        /// 说明：用来确认上层客户端是否为同一个(倘若是同一个,设备记录内存,下次搜索加快速度)
        /// 类型：string
        /// </summary>
        public required string SearchID { get; set; }

        /// <summary>
        /// 【必需】查询结果起始位置
        /// 说明：
        /// - 当记录条数很多时，一次查询不能获取所有记录，指定此位置可查询后续记录
        /// - 若设备支持的最大totalMatches为M个，当前存储N个（N≤M），则合法范围0~N-1
        /// 类型：int
        /// </summary>
        public required int SearchResultPosition { get; set; }

        /// <summary>
        /// 【必需】最大返回记录数
        /// 说明：如值大于设备能力集范围，则按能力集最大值返回，不报错
        /// 类型：int
        /// </summary>
        public required int MaxResults { get; set; }

        /// <summary>
        /// 【可选】人员ID列表
        /// 子类型：EmployeeNo对象数组
        /// 说明：与卡号列表互斥，两者选一
        /// </summary>
        public List<EmployeeNoList>? EmployeeNoList { get; set; }

        /// <summary>
        /// 【可选】卡号列表
        /// 子类型：CardNo对象数组
        /// 说明：与人员ID列表互斥，两者选一
        /// </summary>
        public List<CardNoList>? CardNoList { get; set; }
    }
    /// <summary>
    /// 卡号信息
    /// </summary>
    public class CardNoList
    {
        /// <summary>
        /// 【可选】卡号
        /// 类型：string
        /// </summary>
        public string? CardNo { get; set; }
    }

    /// <summary>
    /// 卡查询结果
    /// </summary>
    public class CardInfoSearch
    {
        /// <summary>
        /// 【只读|必需】搜索记录唯一标识
        /// 类型：string
        /// </summary>
        public required string SearchID { get; set; }

        /// <summary>
        /// 【只读|必需】查询状态
        /// 枚举值：
        /// - OK#查询结束
        /// - MORE#还有数据等待查询
        /// - NO MATCH#没有匹配数据
        /// 类型：string
        /// </summary>
        public required string ResponseStatusStrg { get; set; }

        /// <summary>
        /// 【只读|必需】本次返回记录数
        /// 类型：int
        /// </summary>
        public required int NumOfMatches { get; set; }

        /// <summary>
        /// 【只读|必需】符合条件的总记录数
        /// 类型：int
        /// </summary>
        public required int TotalMatches { get; set; }

        /// <summary>
        /// 【只读|可选】卡信息列表
        /// 子类型：CardInfo对象数组
        /// </summary>
        public List<CardInfo>? CardInfo { get; set; }
    }

    /// <summary>
    /// 卡详细信息
    /// </summary>
    public class CardInfo
    {
        /// <summary>
        /// 【只读|必需】工号（人员ID）
        /// 类型：string
        /// </summary>
        public required string EmployeeNo { get; set; }

        /// <summary>
        /// 【只读|可选】工号生成类型
        /// 枚举值：
        /// - platform#平台生成
        /// - local#本地生成
        /// 说明：
        /// - 仅HEOP使用
        /// - 不返回则默认为平台生成的工号
        /// - 应用场景：当通过输入卡号查询人员信息开启采集时使用
        /// 类型：string
        /// </summary>
        public string? EmployeeNoType { get; set; }

        /// <summary>
        /// 【只读|必需】卡号
        /// 类型：string
        /// </summary>
        public required string CardNo { get; set; }

        /// <summary>
        /// 【只读|必需】卡类型
        /// 枚举值：
        /// - normalCard#普通卡
        /// - patrolCard#巡更卡
        /// - hijackCard#胁迫卡
        /// - superCard#超级卡
        /// - dismissingCard#解除卡
        /// - emergencyCard#应急管理卡
        /// 说明：应急管理卡用于授权临时卡权限，本身不能开门
        /// 类型：string
        /// </summary>
        public required string CardType { get; set; }

        /// <summary>
        /// 【只读|可选】首次认证功能
        /// 说明：表示该卡对于门1、门3、门5有首次认证功能
        /// 格式：逗号分隔的门编号字符串
        /// 类型：string
        /// </summary>
        public string? LeaderCard { get; set; }
        public bool? CheckEmployeeNo { get; set; }
        public bool? CheckCardNo { get; set; }
    }

    public class DeletaUserRequest
    {
        public UserInfoDetailRequest? UserInfoDetail { get; set; }
    }

    /// <summary>
    /// 用户信息操作请求
    /// </summary>
    public class UserInfoDetailRequest
    {
        /// <summary>
        /// 【必需】删除模式
        /// 枚举值：
        /// - all#删除所有
        /// - byEmployeeNo#按工号
        /// 类型：string
        /// </summary>
        public required string Mode { get; set; }

        /// <summary>
        /// 【可选】人员ID列表
        /// 子类型：EmployeeNo对象数组
        /// 范围：无限制
        /// 说明：当Mode为byEmployeeNo时必须提供
        /// </summary>
        public List<EmployeeNoList>? EmployeeNoList { get; set; }

        /// <summary>
        /// 【可选】操作类型
        /// 枚举值：
        /// - byTerminal#按终端操作
        /// - byOrg#按组织操作
        /// - byTerminalOrg#按终端组织操作
        /// 类型：string
        /// </summary>
        public string? OperateType { get; set; }

        /// <summary>
        /// 【可选】终端列表
        /// 子类型：int数组
        /// 范围：无限制
        /// 依赖条件：当OperateType为byTerminal时必须提供
        /// </summary>
        public List<int>? TerminalNoList { get; set; }

        /// <summary>
        /// 【可选】组织列表
        /// 子类型：int数组
        /// 范围：无限制
        /// 依赖条件：当OperateType为byOrg或byTerminalOrg时必须提供
        /// </summary>
        public List<int>? OrgNoList { get; set; }
    }

    /// <summary>
    /// 操作响应结果
    /// </summary>
    public class OperationResponse
    {
        /// <summary>
        /// 【只读|可选】状态码
        /// 范围：无限制
        /// 说明：
        /// - 1表示成功且无特殊状态
        /// - 其他值表示特定状态（必须返回）
        /// 类型：int
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; }

        /// <summary>
        /// 【只读|可选】状态描述
        /// 范围：[1,64]字符
        /// 说明：
        /// - "ok"表示成功且无特殊状态
        /// - 其他情况必须返回具体描述
        /// 类型：string
        /// </summary>
        [JsonPropertyName("statusString")]
        public string? StatusString { get; set; }

        /// <summary>
        /// 【只读|可选】子状态码
        /// 范围：[1,64]字符
        /// 说明：
        /// - "ok"表示成功且无特殊状态
        /// - 其他情况必须返回具体子状态
        /// 类型：string
        /// </summary>
        [JsonPropertyName("subStatusCode")]
        public string? SubStatusCode { get; set; }

        /// <summary>
        /// 【只读|可选】错误码
        /// 范围：无限制
        /// 说明：当StatusCode不为1时，必须返回且与SubStatusCode对应
        /// 类型：int
        /// </summary>
        [JsonPropertyName("errorCode")]
        public int? ErrorCode { get; set; }

        /// <summary>
        /// 【只读|可选】错误信息
        /// 范围：无限制
        /// 说明：
        /// - 当StatusCode不为1时，必须返回
        /// - 允许后续版本迭代中优化丰富内容
        /// 类型：string
        /// </summary>
        [JsonPropertyName("errorMsg")]
        public string? ErrorMsg { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class ProcessDeleteResponse
    {
        public OperationResponse? UserInfoDetailDeleteProcess { get; set; }
    }

    public class SetUpPersonRequest
    {
        public UserInfo? UserInfo { get; set; }
    }

    public class FaceInfoRequest
    {
        /// <summary>
        /// 【可选】图片URL
        /// 类型：string
        /// 范围：无限制
        /// 说明：人脸图片的网络地址或本地路径
        /// </summary>
        public string? FaceURL { get; set; }

        /// <summary>
        /// 【必需】人脸库类型
        /// 枚举值：
        /// - blackFD#名单库
        /// - staticFD#静态库
        /// 约束：最大长度32字符
        /// 类型：string
        /// </summary>
        public required string FaceLibType { get; set; }

        /// <summary>
        /// 【必需】人脸库ID
        /// 类型：string
        /// 约束：最大长度63字节
        /// 说明：多个人脸库用逗号隔开
        /// </summary>
        public required string FDID { get; set; }

        /// <summary>
        /// 【可选】人脸记录ID
        /// 类型：string
        /// 约束：最大长度63字节
        /// 说明：
        /// - 外部传入需保证唯一性（字母数字组合）
        /// - 不传时由设备自动生成（与人员ID一致）
        /// </summary>
        public string? FPID { get; set; }

        /// <summary>
        /// 【可选】是否删除该人脸
        /// 类型：bool
        /// 说明：
        /// - true：删除操作（仅删除时填写）
        /// - false/null：新增或修改操作
        /// </summary>
        public bool? DeleteFP { get; set; }


        /// <summary>
        /// 【可选】人脸类型
        /// 枚举值：
        /// - normalFace#普通人脸（默认）
        /// - patrolFace#巡更人脸
        /// - hijackFace#胁迫人脸
        /// - superFace#超级人脸
        /// 类型：string
        /// </summary>
        public string? FaceType { get; set; }

        /// <summary>
        /// 【可选】是否保存人脸底图
        /// 类型：bool
        /// 说明：true-保存原始图片数据
        /// </summary>
        public bool? SaveFacePic { get; set; }

    }

    public class CardInfoRequest
    {
        /// <summary>
        /// 【必需】卡号信息
        /// 子类型：CardInfo对象
        /// 说明：包含卡号、人员ID等信息
        /// </summary>
        public required CardInfo CardInfo { get; set; }

    }


    /// <summary>
    /// 门状态周计划配置
    /// </summary>
    public class DoorStatusWeekPlanCfg
    {
        /// <summary>
        /// 【必需】门状态周计划使能
        /// 类型：bool
        /// 说明：
        /// - true：启用周计划功能
        /// - false：禁用周计划功能
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// 【可选】开始日期
        /// 类型：string（日期格式）
        /// 格式：yyyy-MM-dd
        /// 说明：
        /// 1. 不下发开始日期和结束日期时，默认每周都生效
        /// 2. 下发开始日期和结束日期时，仅在该时间段内生效
        /// 3. 班牌设备仅支持一个门、一个周计划，直接通过此协议配置
        /// </summary>
        public string? BeginDate { get; set; }

        /// <summary>
        /// 【可选】结束日期
        /// 类型：string（日期格式）
        /// 格式：yyyy-MM-dd
        /// 说明：
        /// 1. 不下发开始日期和结束日期时，默认每周都生效
        /// 2. 下发开始日期和结束日期时，仅在该时间段内生效
        /// 3. 班牌设备仅支持一个门、一个周计划，直接通过此协议配置
        /// </summary>
        public string? EndDate { get; set; }

        /// <summary>
        /// 【必需】周计划参数列表
        /// 子类型：WeekPlanCfg对象数组
        /// 说明：每周各时间段的门状态配置
        /// </summary>
        public List<WeekPlanCfg> WeekPlanCfg { get; set; } = new List<WeekPlanCfg>();
    }

    /// <summary>
    /// 周计划配置项
    /// </summary>
    public class WeekPlanCfg
    {
        /// <summary>
        /// 【必需】星期
        /// 枚举值：
        /// - Monday#星期一
        /// - Tuesday#星期二
        /// - Wednesday#星期三
        /// - Thursday#星期四
        /// - Friday#星期五
        /// - Saturday#星期六
        /// - Sunday#星期日
        /// 类型：string
        /// </summary>
        public string Week { get; set; } = null!;

        /// <summary>
        /// 【必需】时间段编号
        /// 类型：int
        /// 范围：1-8
        /// 说明：一天内最多支持8个时间段
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 【必需】周计划使能
        /// 类型：bool
        /// 说明：
        /// - true：启用该时间段配置
        /// - false：禁用该时间段配置
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// 【必需】门状态计划
        /// 枚举值：
        /// - remainOpen#常开
        /// - remainClosed#常闭
        /// - normal#普通
        /// - sleep#休眠
        /// - invalid#无效
        /// - induction#感应模式
        /// - barrierFree#无障碍模式
        /// 说明：
        /// - 感应模式：红外感应到人员通行时才开门（当前仅支持红外感应）
        /// - 无障碍模式：门翼保持开启，未认证权限时门翼关闭并报警
        /// 类型：string
        /// </summary>
        public string DoorStatus { get; set; } = null!;

        /// <summary>
        /// 【必需】时间节点配置
        /// 类型：TimeSegment
        /// 说明：该时间段的具体起止时间
        /// </summary>
        public TimeSegment TimeSegment { get; set; } = new TimeSegment();
    }

    /// <summary>
    /// 时间节点配置
    /// </summary>
    public class TimeSegment
    {
        /// <summary>
        /// 【必需】开始时间点
        /// 类型：string（时间格式）
        /// 格式：HH:mm:ss
        /// 说明：设备本地时间
        /// </summary>
        public string BeginTime { get; set; } = null!;

        /// <summary>
        /// 【必需】结束时间点
        /// 类型：string（时间格式）
        /// 格式：HH:mm:ss
        /// 说明：设备本地时间
        /// </summary>
        public string EndTime { get; set; } = null!;
    }
}
