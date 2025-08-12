using GrpcService.HKSDK;

namespace GrpcService.Models
{
    public class GrpcServerConfiguration
    {
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

    public class LibraryPathsConfiguration
    {
        public PlatformLibraryConfiguration Windows { get; set; } = new();
        public PlatformLibraryConfiguration Linux { get; set; } = new();
    }

    public class PlatformLibraryConfiguration
    {
        public string BasePath { get; set; } = "";
        public string LibCrypto { get; set; } = "";
        public string LibSsl { get; set; } = "";
        public string LibIconv { get; set; } = "";
        public string LibZ { get; set; } = "";
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
}
