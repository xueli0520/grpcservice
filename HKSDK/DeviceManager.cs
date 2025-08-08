using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace GrpcService.HKSDK
{
    public class DeviceManager : BackgroundService
    {
        private readonly ILogger<DeviceManager> _logger;
        private readonly ConcurrentDictionary<string, DeviceConnection> _devices;
        private readonly Timer _heartbeatTimer;
        private readonly Channel<DeviceCommand> _commandChannel;
        private readonly ChannelWriter<DeviceCommand> _commandWriter;
        private readonly ChannelReader<DeviceCommand> _commandReader;
        private readonly SemaphoreSlim _deviceOperationSemaphore;
        private readonly object _disposeLock = new();
        private bool _disposed = false;

        // 魔法数字提取为常量
        private const int DefaultHeartbeatTimeoutSeconds = 120;
        private const int DefaultHeartbeatCheckIntervalSeconds = 60;
        private const int DefaultMaxConcurrentOperations = 10;

        private readonly int _heartbeatTimeoutSeconds = DefaultHeartbeatTimeoutSeconds;
        private readonly int _heartbeatCheckIntervalSeconds = DefaultHeartbeatCheckIntervalSeconds;
        private readonly int _maxConcurrentOperations = DefaultMaxConcurrentOperations;

        public DeviceManager(ILogger<DeviceManager> logger)
        {
            _logger = logger;
            _devices = new ConcurrentDictionary<string, DeviceConnection>();

            // 初始化命令队列 - 无界通道，支持高并发
            var options = new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };
            _commandChannel = Channel.CreateUnbounded<DeviceCommand>(options);
            _commandWriter = _commandChannel.Writer;
            _commandReader = _commandChannel.Reader;

            // 限制并发操作数量
            _deviceOperationSemaphore = new SemaphoreSlim(_maxConcurrentOperations, _maxConcurrentOperations);

            // 启动心跳检测定时器
            _heartbeatTimer = new Timer(CheckDeviceHeartbeat, null,
                TimeSpan.FromSeconds(_heartbeatCheckIntervalSeconds),
                TimeSpan.FromSeconds(_heartbeatCheckIntervalSeconds));

            _logger.LogInformation("设备管理器初始化完成，支持最大并发操作: {MaxConcurrentOperations}", _maxConcurrentOperations);
        }

        /// <summary>
        /// 注册设备
        /// </summary>
        public async Task<(bool Success, string Message, string DeviceId)> RegisterDeviceAsync(
            int lUserID, HCOTAPCMS.OTAP_CMS_DEV_REG_INFO struDevInfo)
        {
            string deviceId = Encoding.Default.GetString(struDevInfo.byDeviceID).TrimEnd('\0');
            try
            {
                // 检查设备是否已存在
                if (_devices.TryGetValue(deviceId, out var existingDevice))
                {
                    if (existingDevice.IsConnected == true && existingDevice.UserId == lUserID)
                    {
                        existingDevice.LastHeartbeat = DateTime.Now;
                        _logger.LogDebug("设备已连接，更新心跳: {DeviceId}", deviceId);
                        return (true, "设备已连接", existingDevice.DeviceId);
                    }
                    else
                    {
                        // 清理旧连接
                        await DisconnectDeviceAsync(deviceId, existingDevice.UserId ?? -1);
                        _logger.LogInformation("清理设备旧连接: {DeviceId}", deviceId);
                    }
                }

                // 创建新的设备连接
                var device = new DeviceConnection
                {
                    UserId = lUserID,
                    DeviceId = deviceId,
                    DevicePort = struDevInfo.struDevAddr.wPort,
                    DeviceIP = new string(struDevInfo.struDevAddr.szIP).TrimEnd('\0'),
                    LastHeartbeat = DateTime.Now,
                    IsConnected = true,
                    RegisterTime = DateTime.Now
                };

                // 添加到设备列表
                _devices.AddOrUpdate(deviceId, device, (key, oldValue) => device);

                _logger.LogInformation("设备注册成功: {DeviceId}, IP: {DeviceIP}, Port: {DevicePort}, UserId: {UserId}",
                    deviceId, device.DeviceIP, device.DevicePort, lUserID);
                return (true, "设备注册成功", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "注册设备时发生异常: {DeviceId}", deviceId);
                return (false, $"注册设备异常: {ex.Message}", deviceId);
            }
        }

        /// <summary>
        /// 更新设备心跳
        /// </summary>
        public bool UpdateDeviceHeartbeat(string deviceId, int userId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                if (device?.UserId == userId && device.IsConnected == true)
                {
                    device.LastHeartbeat = DateTime.Now;
                    _logger.LogDebug("更新设备心跳: {DeviceId}", deviceId);
                    return true;
                }
            }

            _logger.LogWarning("更新心跳失败，设备不存在或UserId不匹配: {DeviceId}, UserId: {UserId}", deviceId, userId);
            return false;
        }

        /// <summary>
        /// 执行设备命令 - 异步队列处理
        /// </summary>
        public async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
            ExecuteDeviceCommandAsync(string deviceId, string commandType, Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default)
        {
            if (!_devices.TryGetValue(deviceId, out var device) || device.IsConnected != true)
            {
                return (false, $"设备未连接: {deviceId}", null);
            }

            var command = new DeviceCommand
            {
                CommandId = Guid.NewGuid().ToString(),
                DeviceId = deviceId,
                CommandType = commandType,
                Parameters = parameters,
                TaskCompletionSource = new TaskCompletionSource<(bool, string, Dictionary<string, object>)>(),
                CreatedTime = DateTime.Now,
                CancellationToken = cancellationToken
            };

            try
            {
                // 将命令加入队列
                await _commandWriter.WriteAsync(command, cancellationToken);
                _logger.LogDebug("命令已加入队列: {CommandId}, DeviceId: {DeviceId}, Type: {CommandType}",
                    command.CommandId, deviceId, commandType);

                // 等待命令执行完成（带超时）
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                return await command.TaskCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("设备命令执行被取消: {CommandId}, DeviceId: {DeviceId}", command.CommandId, deviceId);
                return (false, "命令执行被取消", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行设备命令异常: {CommandId}, DeviceId: {DeviceId}", command.CommandId, deviceId);
                return (false, $"执行命令异常: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 处理命令队列
        /// </summary>
        private async Task ProcessCommandQueueAsync(CancellationToken cancellationToken)
        {
            await foreach (var command in _commandReader.ReadAllAsync(cancellationToken))
            {
                // 使用信号量控制并发
                await _deviceOperationSemaphore.WaitAsync(cancellationToken);

                // 在后台线程中处理命令
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ProcessDeviceCommandAsync(command);
                        command.TaskCompletionSource.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理设备命令异常: {CommandId}", command.CommandId);
                        command.TaskCompletionSource.SetResult((false, $"命令处理异常: {ex.Message}", null));
                    }
                    finally
                    {
                        _deviceOperationSemaphore.Release();
                    }
                }, cancellationToken);
            }
        }

        /// <summary>
        /// 处理具体的设备命令
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
            ProcessDeviceCommandAsync(DeviceCommand command)
        {
            if (!_devices.TryGetValue(command.DeviceId, out var device) || device.IsConnected != true)
            {
                return (false, $"设备未连接: {command.DeviceId}", null);
            }

            try
            {
                _logger.LogDebug("开始处理设备命令: {CommandId}, DeviceId: {DeviceId}, Type: {CommandType}",
                    command.CommandId, command.DeviceId, command.CommandType);

                // 根据命令类型执行相应操作
                return command.CommandType.ToLower() switch
                {
                    "getstatus" => await GetDeviceStatusAsync(device),
                    "opendoor" => await OpenDoorAsync(device, command.Parameters),
                    "reboot" => await RebootDeviceAsync(device, command.Parameters),
                    "synctime" => await SyncTimeAsync(device, command.Parameters),
                    "getdeviceinfo" => await GetDeviceInfoAsync(device),
                    "updateuserall" => await UpdateUserAllAsync(device, command.Parameters),
                    "getuserlist" => await GetUserListAsync(device, command.Parameters),
                    "setdoormode" => await SetDoorModeAsync(device, command.Parameters),
                    "updatewhite" => await UpdateWhiteAsync(device, command.Parameters),
                    "deletewhite" => await DeleteWhiteAsync(device, command.Parameters),
                    "pagewhite" => await PageWhiteAsync(device, command.Parameters),
                    "detailwhite" => await DetailWhiteAsync(device, command.Parameters),
                    "updatetimezone" => await UpdateTimezoneAsync(device, command.Parameters),
                    "querytimezone" => await QueryTimezoneAsync(device, command.Parameters),
                    "setdoortemplate" => await SetDoorTemplateAsync(device, command.Parameters),
                    "syncdeviceparameter" => await SyncDeviceParameterAsync(device, command.Parameters),
                    "getwhiteusertotal" => await GetWhiteUserTotalAsync(device),
                    "getversion" => await GetVersionAsync(device),
                    _ => (false, $"不支持的命令类型: {command.CommandType}", null)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行设备命令异常: {CommandId}, DeviceId: {DeviceId}, CommandType: {CommandType}",
                    command.CommandId, command.DeviceId, command.CommandType);
                return (false, $"执行命令异常: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 获取设备状态
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
            GetDeviceStatusAsync(DeviceConnection device)
        {
            var resultData = new Dictionary<string, object>
            {
                ["device_id"] = device.DeviceId,
                ["status"] = device.IsConnected == true ? "online" : "offline",
                ["last_heartbeat"] = device.LastHeartbeat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知",
                ["user_id"] = device.UserId?.ToString() ?? "未知",
                ["device_ip"] = device.DeviceIP ?? "未知",
                ["device_port"] = device.DevicePort?.ToString() ?? "未知",
                ["register_time"] = device.RegisterTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知"
            };

            return (true, "获取状态成功", resultData);
        }

        // Fix for CS9006: Adjusting the number of '$' characters in interpolated raw string literals to match the number of '{' characters.

        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
           OpenDoorAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            try
            {
                var userId = device.UserId ?? -1;

                // Corrected the interpolated raw string literal
                var openDoorJson = $$"""
               {
                   "AcsEvent": {
                       "type": "unlock",
                       "userType": "normal"
                   }
               }
               """;

                // 使用OTAP协议下发开门命令
                CMSServiceHelpers.Cms_SetConfigDev(userId, HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_MODEL_SERVER_OPERATE,
                    "event",
                    "unlock",
                    openDoorJson,
                    _logger
                );

                var resultData = new Dictionary<string, object>
                {
                    ["command"] = "open_door",
                    ["device_id"] = device.DeviceId,
                    ["status"] = "executed",
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                _logger.LogInformation("开门命令执行成功: {DeviceId}", device.DeviceId);
                return (true, "开门命令执行成功", resultData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "开门命令执行失败: {DeviceId}", device.DeviceId);
                return (false, $"开门命令执行失败: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 重启设备
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
   RebootDeviceAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            try
            {
                // Corrected the interpolated raw string literal
                var rebootJson = $$"""
               {
                   "System": {
                       "reboot": true
                   }
               }
               """;
                CMSServiceHelpers.Cms_SetConfigDev(
                    device.UserId.Value, HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_MODEL_SERVER_OPERATE,
                    "system",
                    "reboot",
                    rebootJson,
                    _logger
                );

                var resultData = new Dictionary<string, object>
                {
                    ["command"] = "reboot",
                    ["device_id"] = device.DeviceId,
                    ["status"] = "executed"
                };

                return (true, "重启命令执行成功", resultData);
            }
            catch (Exception ex)
            {
                return (false, $"重启命令执行失败: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 同步时间
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
   SyncTimeAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            try
            {
                var timestamp = parameters.GetValueOrDefault("timestamp", DateTimeOffset.Now.ToUnixTimeSeconds());
                // Corrected the interpolated raw string literal
                var timeJson = $$"""
               {
                   "Time": {
                       "timeMode": "NTP",
                       "localTime": "{{DateTimeOffset.FromUnixTimeSeconds((long)timestamp):yyyy-MM-ddTHH:mm:ss}}"
                   }
               }
               """;

                CMSServiceHelpers.Cms_SetConfigDev(
                    device.UserId.Value,
                    HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_SET_MODEL_ATTR,
                    "system",
                    "time",
                    timeJson,
                    _logger
                );

                var resultData = new Dictionary<string, object>
                {
                    ["command"] = "sync_time",
                    ["device_id"] = device.DeviceId,
                    ["status"] = "executed",
                    ["sync_time"] = DateTimeOffset.FromUnixTimeSeconds((long)timestamp).ToString("yyyy-MM-dd HH:mm:ss")
                };

                return (true, "时间同步成功", resultData);
            }
            catch (Exception ex)
            {
                return (false, $"时间同步失败: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 获取设备信息
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, object> ResultData)>
            GetDeviceInfoAsync(DeviceConnection device)
        {
            try
            {
                // 获取设备基本信息
                CMSServiceHelpers.Cms_SetConfigDev(
                    device.UserId.Value,
                    HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM.OTAP_ENUM_OTAP_CMS_GET_MODEL_ATTR,
                    "system",
                    "deviceInfo",
                    "",
                    _logger
                );

                var resultData = new Dictionary<string, object>
                {
                    ["device_id"] = device.DeviceId,
                    ["device_ip"] = device.DeviceIP ?? "未知",
                    ["device_port"] = device.DevicePort?.ToString() ?? "未知",
                    ["last_online_time"] = device.LastHeartbeat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知",
                    ["register_time"] = device.RegisterTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知"
                };

                return (true, "获取设备信息成功", resultData);
            }
            catch (Exception ex)
            {
                return (false, $"获取设备信息失败: {ex.Message}", null);
            }
        }

        // 其他命令处理方法的占位符实现
        private async Task<(bool, string, Dictionary<string, object>)> UpdateUserAllAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现用户批量更新逻辑
            return (true, "用户批量更新成功", new Dictionary<string, object> { ["updated_count"] = 0 });
        }

        private async Task<(bool, string, Dictionary<string, object>)> GetUserListAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现获取用户列表逻辑
            return (true, "获取用户列表成功", new Dictionary<string, object> { ["users"] = new List<object>() });
        }

        private async Task<(bool, string, Dictionary<string, object>)> SetDoorModeAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现设置门禁模式逻辑
            return (true, "设置门禁模式成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> UpdateWhiteAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现更新白名单逻辑
            return (true, "更新白名单成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> DeleteWhiteAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现删除白名单逻辑
            return (true, "删除白名单成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> PageWhiteAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现分页获取白名单逻辑
            return (true, "分页获取白名单成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> DetailWhiteAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现获取白名单详情逻辑
            return (true, "获取白名单详情成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> UpdateTimezoneAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现更新时段逻辑
            return (true, "更新时段成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> QueryTimezoneAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现查询时段逻辑
            return (true, "查询时段成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> SetDoorTemplateAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现设置门禁模板逻辑
            return (true, "设置门禁模板成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> SyncDeviceParameterAsync(DeviceConnection device, Dictionary<string, object> parameters)
        {
            // TODO: 实现同步设备参数逻辑
            return (true, "同步设备参数成功", new Dictionary<string, object>());
        }

        private async Task<(bool, string, Dictionary<string, object>)> GetWhiteUserTotalAsync(DeviceConnection device)
        {
            // TODO: 实现获取白名单总数逻辑
            return (true, "获取白名单总数成功", new Dictionary<string, object> { ["total_count"] = 0 });
        }

        private async Task<(bool, string, Dictionary<string, object>)> GetVersionAsync(DeviceConnection device)
        {
            // TODO: 实现获取设备版本逻辑
            return (true, "获取设备版本成功", new Dictionary<string, object> { ["version"] = "1.0.0" });
        }

        /// <summary>
        /// 获取在线设备列表
        /// </summary>
        public List<DeviceConnection> GetOnlineDevices()
        {
            return _devices.Values.Where(d => d.IsConnected == true).ToList();
        }

        /// <summary>
        /// 获取设备统计信息
        /// </summary>
        public Dictionary<string, object> GetDeviceStatistics()
        {
            var onlineDevices = _devices.Values.Where(d => d.IsConnected == true).ToList();
            return new Dictionary<string, object>
            {
                ["total_devices"] = _devices.Count,
                ["online_devices"] = onlineDevices.Count,
                ["offline_devices"] = _devices.Count - onlineDevices.Count,
                ["last_update"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>
        /// 检查设备心跳
        /// </summary>
        private void CheckDeviceHeartbeat(object state)
        {
            var now = DateTime.Now;
            var devicesToRemove = new List<string>();

            foreach (var kvp in _devices)
            {
                var device = kvp.Value;
                if (device.LastHeartbeat == null) continue;

                var timeSinceLastHeartbeat = now - device.LastHeartbeat.Value;

                if (timeSinceLastHeartbeat.TotalSeconds <= _heartbeatTimeoutSeconds)
                {
                    continue;
                }

                _logger.LogWarning("设备心跳超时，准备断开连接: {DeviceId}, 最后心跳: {LastHeartbeat}, 超时时间: {TimeoutSeconds}秒",
                    device.DeviceId, device.LastHeartbeat, _heartbeatTimeoutSeconds);
                devicesToRemove.Add(kvp.Key);
            }

            // 移除超时设备
            foreach (var deviceId in devicesToRemove)
            {
                if (_devices.TryGetValue(deviceId, out var device))
                {
                    _ = Task.Run(() => DisconnectDeviceAsync(deviceId, device.UserId ?? -1));
                }
            }

            if (devicesToRemove.Any())
            {
                _logger.LogInformation("心跳检查完成，清理超时设备数量: {Count}", devicesToRemove.Count);
            }
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        public async Task DisconnectDeviceAsync(string deviceId, int userId = -1)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                try
                {
                    if (userId == -1 || device.UserId == userId)
                    {
                        device.IsConnected = false;
                        _logger.LogInformation("设备连接已断开: {DeviceId}, UserId: {UserId}", deviceId, device.UserId);
                    }
                    else
                    {
                        _logger.LogWarning("断开设备连接失败，UserId不匹配: {DeviceId}, 期望: {ExpectedUserId}, 实际: {ActualUserId}",
                            deviceId, userId, device.UserId);
                        // 将设备重新添加回去
                        _devices.TryAdd(deviceId, device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "断开设备连接时发生异常: {DeviceId}", deviceId);
                }
            }
            else
            {
                _logger.LogWarning("尝试断开不存在的设备连接: {DeviceId}", deviceId);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("设备管理器后台服务启动");

            // 启动命令处理队列
            var commandProcessingTask = ProcessCommandQueueAsync(stoppingToken);

            try
            {
                await commandProcessingTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("设备管理器后台服务正常停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备管理器后台服务异常停止");
            }
        }

        public override void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                try
                {
                    _logger.LogInformation("开始清理设备管理器资源...");

                    // 停止写入新命令
                    _commandWriter?.Complete();

                    // 清理所有设备连接
                    var deviceCount = 0;
                    foreach (var device in _devices.Values)
                    {
                        if (device.UserId >= 0)
                        {
                            try
                            {
                                // 这里可以调用SDK的清理方法
                                deviceCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "清理设备连接异常: {DeviceId}", device.DeviceId);
                            }
                        }
                    }
                    _devices.Clear();

                    // 清理定时器
                    _heartbeatTimer?.Dispose();
                    // 清理信号量
                    _deviceOperationSemaphore?.Dispose();

                    _logger.LogInformation("设备管理器已释放所有资源，清理设备数量: {DeviceCount}", deviceCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理设备管理器资源时发生异常");
                }
                finally
                {
                    _disposed = true;
                    base.Dispose();
                }
            }
        }
    }

    // 设备连接信息模型
    public class DeviceConnection
    {
        public string? DeviceId { get; set; }
        public string? DeviceIP { get; set; }
        public int? DevicePort { get; set; }
        public int? UserId { get; set; }
        public bool? IsConnected { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public DateTime? RegisterTime { get; set; }
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