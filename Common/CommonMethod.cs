using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace GrpcService.Common
{
    /// <summary>
    /// 通用方法类
    /// </summary>
    public static class CommonMethod
    {
        private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _configCache = new();
        private static readonly object _configFileLock = new();
        private static ILogger? _logger;

        /// <summary>
        /// 初始化日志器
        /// </summary>
        public static void InitializeLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 创建目录（如果不存在）
        /// </summary>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _logger?.LogDebug("创建目录: {DirectoryPath}", directoryPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建目录失败: {DirectoryPath}", directoryPath);
                return false;
            }
        }
    }

    /// <summary>
    /// 扩展方法类
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 截断字符串到指定长度
        /// </summary>
        public static string Truncate(this string? value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? "";

            var truncatedLength = maxLength - suffix.Length;
            return truncatedLength > 0 ? value.Substring(0, truncatedLength) + suffix : value.Substring(0, maxLength);
        }
    }
}