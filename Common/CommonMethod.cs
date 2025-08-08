using System.Collections.Concurrent;
using System.Text;

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
        /// 从IConfiguration读取配置值
        /// </summary>
        public static T GetConfigurationValue<T>(IConfiguration configuration, string key, T defaultValue = default!)
        {
            try
            {
                var value = configuration[key];
                if (string.IsNullOrEmpty(value))
                {
                    _logger?.LogWarning("配置项 {Key} 未找到，使用默认值: {DefaultValue}", key, defaultValue);
                    return defaultValue;
                }

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)value;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "读取配置项失败: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// 获取平台特定的库文件路径
        /// </summary>
        public static string GetPlatformLibraryPath(IConfiguration configuration, string libraryName)
        {
            try
            {
                var platform = Environment.OSVersion.Platform == PlatformID.Win32NT ? "Windows" : "Linux";
                var basePath = configuration[$"LibraryPaths:{platform}:BasePath"];
                var fileName = configuration[$"LibraryPaths:{platform}:{GetLibraryConfigKey(libraryName)}"];

                if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fileName))
                {
                    _logger?.LogWarning("未找到平台 {Platform} 的库文件配置 {LibraryName}", platform, libraryName);
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", platform, $"{libraryName}.dll");
                }

                return Path.Combine(basePath, fileName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取库文件路径失败: {LibraryName}", libraryName);
                return "";
            }
        }

        private static string GetLibraryConfigKey(string libraryName)
        {
            return libraryName.ToLower() switch
            {
                "libcrypto" => "LibCrypto",
                "libssl" => "LibSsl",
                "libiconv2" => "LibIconv",
                "libz" => "LibZ",
                _ => libraryName
            };
        }

        /// <summary>
        /// 验证配置文件必需项
        /// </summary>
        public static (bool IsValid, List<string> Errors) ValidateConfiguration(IConfiguration configuration)
        {
            var errors = new List<string>();
            var isValid = true;

            var requiredKeys = new[]
            {
                "HikDevice:CmsServerIP",
                "HikDevice:CmsServerPort",
                "HikDevice:DasServerIP",
                "HikDevice:DasServerPort",
                "HikDevice:PicServerIP",
                "HikDevice:PicServerPort",
                "HikDevice:OTAPKey",
                "GrpcServer:Host",
                "GrpcServer:Port"
            };

            foreach (var key in requiredKeys)
            {
                var value = configuration[key];
                if (string.IsNullOrEmpty(value))
                {
                    errors.Add($"缺少必需的配置项: {key}");
                    isValid = false;
                }
            }

            // 验证端口号
            var portKeys = new[]
            {
                "HikDevice:CmsServerPort",
                "HikDevice:DasServerPort",
                "HikDevice:PicServerPort",
                "GrpcServer:Port"
            };

            foreach (var portKey in portKeys)
            {
                var portValue = configuration[portKey];
                if (!string.IsNullOrEmpty(portValue) &&
                    (!int.TryParse(portValue, out var port) || port <= 0 || port > 65535))
                {
                    errors.Add($"无效的端口号 {portKey}: {portValue}");
                    isValid = false;
                }
            }

            return (isValid, errors);
        }

        /// <summary>
        /// 安全地将字符串复制到字符数组
        /// </summary>
        public static void SafeCopyTo(this string source, int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            if (source == null || destination == null) return;

            var actualCount = Math.Min(count, Math.Min(source.Length - sourceIndex, destination.Length - destinationIndex));
            if (actualCount > 0)
            {
                source.CopyTo(sourceIndex, destination, destinationIndex, actualCount);
            }
        }

        /// <summary>
        /// 获取环境信息
        /// </summary>
        public static Dictionary<string, object> GetEnvironmentInfo()
        {
            try
            {
                return new Dictionary<string, object>
                {
                    ["os_description"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    ["os_architecture"] = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                    ["process_architecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    ["framework_description"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    ["machine_name"] = Environment.MachineName,
                    ["user_name"] = Environment.UserName,
                    ["processor_count"] = Environment.ProcessorCount,
                    ["working_set"] = Environment.WorkingSet,
                    ["current_directory"] = Environment.CurrentDirectory,
                    ["clr_version"] = Environment.Version.ToString(),
                    ["is_64bit_process"] = Environment.Is64BitProcess,
                    ["is_64bit_os"] = Environment.Is64BitOperatingSystem
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取环境信息失败");
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            return bytes switch
            {
                >= TB => $"{(double)bytes / TB:F2} TB",
                >= GB => $"{(double)bytes / GB:F2} GB",
                >= MB => $"{(double)bytes / MB:F2} MB",
                >= KB => $"{(double)bytes / KB:F2} KB",
                _ => $"{bytes} B"
            };
        }

        /// <summary>
        /// 生成唯一ID
        /// </summary>
        public static string GenerateUniqueId(string prefix = "")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = new Random().Next(1000, 9999);
            return $"{prefix}{timestamp}{random}";
        }

        /// <summary>
        /// 安全的类型转换
        /// </summary>
        public static T SafeConvert<T>(object? value, T defaultValue = default!)
        {
            if (value == null) return defaultValue;

            try
            {
                if (value is T directValue)
                {
                    return directValue;
                }

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)value.ToString()!;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "类型转换失败，使用默认值: {Value} -> {TargetType}", value, typeof(T).Name);
                return defaultValue;
            }
        }

        /// <summary>
        /// 验证IP地址格式
        /// </summary>
        public static bool IsValidIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            return System.Net.IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// 验证端口号
        /// </summary>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
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

        /// <summary>
        /// 重试执行操作
        /// </summary>
        public static async Task<T> RetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int delayMs = 1000,
            string operationName = "操作")
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt <= maxRetries)
                    {
                        _logger?.LogWarning(ex, "{OperationName} 第 {Attempt} 次尝试失败，{DelayMs}ms 后重试",
                            operationName, attempt, delayMs);
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        _logger?.LogError(ex, "{OperationName} 在 {MaxRetries} 次重试后仍然失败",
                            operationName, maxRetries);
                    }
                }
            }

            throw lastException ?? new InvalidOperationException($"{operationName} 失败");
        }

        /// <summary>
        /// 执行带超时的操作
        /// </summary>
        public static async Task<T> WithTimeoutAsync<T>(
            Task<T> operation,
            int timeoutMs,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await operation.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"操作在 {timeoutMs}ms 内未完成");
            }
        }
    }

    /// <summary>
    /// 扩展方法类
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 安全获取字典值
        /// </summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue = default!) where TKey : notnull
        {
            return dictionary != null && dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 安全获取对象字典值
        /// </summary>
        public static object GetValueOrDefault(
            this Dictionary<string, object> dictionary,
            string key,
            object defaultValue = default!)
        {
            return dictionary != null && dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 将对象转换为指定类型
        /// </summary>
        public static T ToType<T>(this object? value, T defaultValue = default!)
        {
            return CommonMethod.SafeConvert(value, defaultValue);
        }

        /// <summary>
        /// 检查字符串是否为空或空白
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

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

        /// <summary>
        /// 将时间转换为Unix时间戳（秒）
        /// </summary>
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }

        /// <summary>
        /// 将Unix时间戳（秒）转换为时间
        /// </summary>
        public static DateTime FromUnixTimeSeconds(this long unixTimeSeconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds).DateTime;
        }
    }
}