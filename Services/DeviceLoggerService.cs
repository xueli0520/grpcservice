using System.Collections.Concurrent;

namespace GrpcService.Services
{
    public class DeviceLoggerService : IDeviceLoggerService, IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, ILogger> _deviceLoggers;
        private readonly object _lock = new object();
        private readonly object _disposeLock = new();
        private bool _disposed = false;

        public DeviceLoggerService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _deviceLoggers = new ConcurrentDictionary<string, ILogger>();
        }

        public ILogger GetDeviceLogger(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));
            }

            return _deviceLoggers.GetOrAdd(deviceId, id =>
            {
                lock (_lock)
                {
                    // 创建设备专用日志配置
                    var loggerName = $"Device.{id}";
                    return _loggerFactory.CreateLogger(loggerName);
                }
            });
        }

        public void RemoveDeviceLogger(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;

            _deviceLoggers.TryRemove(deviceId, out _);
        }

        public void LogDeviceInfo(string deviceId, string message, params object[] args)
        {
            var logger = GetDeviceLogger(deviceId);
            logger.LogInformation("[{DeviceId}] " + message, deviceId, args);
        }

        public void LogDeviceWarning(string deviceId, string message, params object[] args)
        {
            var logger = GetDeviceLogger(deviceId);
            logger.LogWarning("[{DeviceId}] " + message, deviceId, args);
        }

        public void LogDeviceError(string deviceId, Exception? exception, string message, params object[] args)
        {
            var logger = GetDeviceLogger(deviceId);
            logger.LogError(exception, "[{DeviceId}] " + message, deviceId, args);
        }

        public void LogDeviceDebug(string deviceId, string message, params object[] args)
        {
            var logger = GetDeviceLogger(deviceId);
            logger.LogDebug("[{DeviceId}] " + message, deviceId, args);
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _deviceLoggers.Clear();
                    _disposed = true;
                }
            }
        }
    }
}
