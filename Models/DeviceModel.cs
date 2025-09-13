using GrpcService.Infrastructure;

namespace GrpcService.Models
{
    // 设备连接信息模型
    public class DeviceConnection(string deviceId, string? deviceIP, int? devicePort, int userId, DeviceLoggerService deviceLogger)
    {
        public string DeviceId { get; set; } = deviceId;
        public string? DeviceIP { get; set; } = deviceIP;
        public int? DevicePort { get; set; } = devicePort;
        public int UserId { get; set; } = userId;
        public bool? IsConnected { get; set; } = true;
        public DateTime? LastHeartbeat { get; set; } = DateTime.Now;
        public DateTime? RegisterTime { get; set; } = DateTime.Now;
        public bool Register { get; set; } = false;
        public DeviceLoggerService? DeviceLogger { get; set; } = deviceLogger;
    }

    // 设备命令模型
    public class DeviceCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public TaskCompletionSource<(bool Success, string Message, Dictionary<string, object> ResultData)> TaskCompletionSource { get; set; } = null!;
        public DateTime CreatedTime { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
