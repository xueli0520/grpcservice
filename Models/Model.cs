using GrpcService.HKSDK;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace GrpcService.Models
{
    public class GrpcServerConfiguration
    {
        public bool? Single { get; set; }
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 5000;
        public int MaxReceiveMessageSize { get; set; } = 4194304;
        public int MaxSendMessageSize { get; set; } = 4194304;
        public int MaxConcurrentCalls { get; set; } = 100;
        public bool UseTls { get; set; } = false;
        public bool RequireClientCertificate { get; set; } = false;
        public string? ServerCertificatePath { get; set; }
        public string? ServerCertificatePassword { get; set; }
        public string? ClientCertificateAuthorityPath { get; set; }
        public int? HealthPort { get; set; }
    }

    public class HikDeviceConfiguration
    {
        public string CmsServerIP { get; set; } = "0.0.0.0";
        public int CmsServerPort { get; set; } = 8000;
        public string DasServerIP { get; set; } = "127.0.0.1";
        public int DasServerPort { get; set; } = 8001;
        public string PicServerIP { get; set; } = "127.0.0.1";
        public int PicServerPort { get; set; } = 9000;
        public string OTAPKey { get; set; } = "DefaultOTAPKey123456";
        public StorageConfiguration Storage { get; set; } = new();
        public int HeartbeatTimeoutSeconds { get; set; } = 120;
        public int HeartbeatCheckIntervalSeconds { get; set; } = 60;
        public int MaxConcurrentOperations { get; set; } = 10;
        public int MaxConcurrentRequests { get; set; } = 20;
        public int QueueCapacity { get; set; } = 1000;
        public int CommandTimeoutMinutes { get; set; } = 2;
    }

    public class StorageConfiguration
    {
        public string Bucket { get; set; } = "otapsstest";
        public string AccessKey { get; set; } = "HCSx5ZO0Ik419x4P23L5JerQ475O213";
        public string SecretKey { get; set; } = "4y8f4V9xn5454b919xaT8Bv2274r0O25";
        public string Region { get; set; } = "Local";
    }


    public class GrpcRequestItem<TRequest, TResponse>
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string DeviceId { get; set; } = "";
        public TRequest Request { get; set; } = default!;
        public TaskCompletionSource<TResponse> TaskCompletionSource { get; set; } = new();
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public CancellationToken CancellationToken { get; set; }
        public string RequestType { get; set; } = "";
        public Func<TRequest, CancellationToken, Task<TResponse>> Handler { get; set; } = null!;
    }
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

    public class GrpcUserInfo
    {
        public UserInfoCount? UserInfoCount { get; set; } = new UserInfoCount();
    }

    public class UserInfoCount
    {
        public int userNumber { get; set; }
        public int bindFaceUserNumber { get; set; }
        public int bindFingerprintUserNumber { get; set; }
        public int bindCardUserNumber { get; set; }
    }
}
