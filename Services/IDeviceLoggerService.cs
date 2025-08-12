namespace GrpcService.Services
{
    public interface IDeviceLoggerService
    {
        void LogDeviceInfo(string deviceId, string message, params object[] args);
        void LogDeviceWarning(string deviceId, string message, params object[] args);
        void LogDeviceError(string deviceId, Exception? exception, string message, params object[] args);
        void LogDeviceDebug(string deviceId, string message, params object[] args);
    }
}
