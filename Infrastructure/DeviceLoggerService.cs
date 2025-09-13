using System.Collections.Concurrent;

namespace GrpcService.Infrastructure
{
    public class DeviceLoggerService : IDeviceLoggerService, IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, ILogger> _deviceLoggers;
        private readonly BlockingCollection<(string DeviceId, LogLevel Level, Exception? Exception, string Message, object[] Args)> _logQueue;
        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public DeviceLoggerService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _deviceLoggers = new ConcurrentDictionary<string, ILogger>();
            _logQueue = [.. new ConcurrentQueue<(string, LogLevel, Exception?, string, object[])>()];
            _cts = new CancellationTokenSource();

            _workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "DeviceLoggerWorker"
            };
            _workerThread.Start();
        }

        private void ProcessQueue()
        {
            try
            {
                foreach (var item in _logQueue.GetConsumingEnumerable(_cts.Token))
                {
                    ILogger logger;
                    if ( !string.IsNullOrWhiteSpace(item.DeviceId))
                    {
                        logger = GetDeviceLogger(item.DeviceId);
                    }
                    else
                    {
                        logger = _loggerFactory.CreateLogger("DefaultLogger");
                    }

                    string msg = $"[{item.DeviceId}] {item.Message}";

                    switch (item.Level)
                    {
                        case LogLevel.Information:
                            logger.LogInformation(item.Exception, msg, item.Args);
                            break;
                        case LogLevel.Warning:
                            logger.LogWarning(item.Exception, msg, item.Args);
                            break;
                        case LogLevel.Error:
                            logger.LogError(item.Exception, msg, item.Args);
                            break;
                        case LogLevel.Debug:
                            logger.LogDebug(item.Exception, msg, item.Args);
                            break;
                        default:
                            logger.Log(item.Level, item.Exception, msg, item.Args);
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                
            }
        }

        public ILogger GetDeviceLogger(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

            return _deviceLoggers.GetOrAdd(deviceId, id =>
            {
                var loggerName = $"Device.{id}";
                return _loggerFactory.CreateLogger(loggerName);
            });
        }

        private void EnqueueLog(string deviceId, LogLevel level, Exception? ex, string message, object[] args)
        {
            if (_disposed) return;
            _logQueue.Add((deviceId, level, ex, message, args));
        }

        public void LogDeviceInfo(string deviceId, string message, params object[] args)
            => EnqueueLog(deviceId, LogLevel.Information, null, message, args);

        public void LogDeviceWarning(string deviceId, string message, params object[] args)
            => EnqueueLog(deviceId, LogLevel.Warning, null, message, args);

        public void LogDeviceError(string deviceId, Exception? exception, string message, params object[] args)
            => EnqueueLog(deviceId, LogLevel.Error, exception, message, args);

        public void LogDeviceDebug(string deviceId, string message, params object[] args)
            => EnqueueLog(deviceId, LogLevel.Debug, null, message, args);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _logQueue.CompleteAdding();

            try
            {
                _workerThread.Join(1000);
            }
            catch { }

            _logQueue.Dispose();
            _cts.Dispose();
        }

        public void RemoveDeviceLogger(string deviceId)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                _deviceLoggers.TryRemove(deviceId, out _);
            }
        }
    }
}
