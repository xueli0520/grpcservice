using GrpcService.HKSDK.service;

namespace GrpcService.Models
{
    public class GrpcRequestMessage
    {
        public string? RequestType { get; set; }
        public required string DeviceId { get; set; }
        public object? RequestData { get; set; }
        public TaskCompletionSource<object>? CompletionSource { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // 服务健康状态
    public class ServiceHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string? Message { get; set; }
        public DeviceManagerStatistics? Statistics { get; set; }
        public int GrpcQueueLength { get; set; }
        public int ConcurrentRequests { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
