using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace GrpcService.HKSDK.service
{
    // 配置选项
    public class DeviceManagerOptions
    {
        public int MaxQueueSize { get; set; } = 10000;
        public int BatchSize { get; set; } = 100;
        public int ProcessingIntervalMs { get; set; } = 50;
        public int MaxConcurrentProcessors { get; set; } = 10;
        public int HeartbeatTimeoutSeconds { get; set; } = 120;
        public int ConnectionPoolSize { get; set; } = 100;
        public int CleanupIntervalMinutes { get; set; } = 5;
    }

    // 设备消息基类
    public abstract class DeviceMessage
    {
        public required string DeviceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public MessagePriority Priority { get; set; }
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
    }

    // 消息优先级
    public enum MessagePriority
    {
        Critical = 0,    // 紧急事件、报警
        High = 1,        // 设备命令
        Normal = 2,      // 状态查询
        Low = 3          // 心跳、日志
    }

    // 设备命令消息
    public class DeviceCommandMessage : DeviceMessage
    {
        public required string Command { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = [];
        public TaskCompletionSource<DeviceCommandResult>? CompletionSource { get; set; }
    }

    // 设备心跳消息
    public class DeviceHeartbeatMessage : DeviceMessage
    {
        public int UserId { get; set; }
    }

    // 设备事件消息
    public class DeviceEventMessage : DeviceMessage
    {
        public string? EventType { get; set; }
        public string? EventData { get; set; }
    }

    // 命令执行结果
    public class DeviceCommandResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object> Data { get; set; } = [];
        public string? ErrorCode { get; set; }
    }


    // 优化后的设备管理器
    public class OptimizedDeviceManager : BackgroundService
    {
        private readonly ILogger<OptimizedDeviceManager> _logger;
        private readonly DeviceManagerOptions _options;
        private readonly ConcurrentDictionary<string, DeviceConnection> _devices;
        private readonly ConcurrentDictionary<string, Channel<DeviceMessage>> _deviceQueues;
        private readonly Channel<DeviceMessage> _globalQueue;
        private readonly Timer _cleanupTimer;
        private readonly Timer _healthCheckTimer;
        private readonly SemaphoreSlim _processingLimiter;

        public OptimizedDeviceManager(
            ILogger<OptimizedDeviceManager> logger,
            IOptions<DeviceManagerOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _devices = new ConcurrentDictionary<string, DeviceConnection>();
            _deviceQueues = new ConcurrentDictionary<string, Channel<DeviceMessage>>();

            // 创建全局队列用于统一调度
            var queueOptions = new BoundedChannelOptions(_options.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _globalQueue = Channel.CreateBounded<DeviceMessage>(queueOptions);

            _processingLimiter = new SemaphoreSlim(_options.MaxConcurrentProcessors);

            // 定时清理过期连接
            _cleanupTimer = new Timer(CleanupExpiredConnections, null,
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));

            // 设备健康检查
            _healthCheckTimer = new Timer(CheckDeviceHealth, null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            _logger.LogInformation("优化设备管理器初始化完成");
        }

        // 注册设备（优化版本）
        public async Task<(bool Success, string Message, string DeviceId)> RegisterDeviceAsync(
            int lUserID, HCOTAPCMS.OTAP_CMS_DEV_REG_INFO struDevInfo)
        {
            string deviceId = Encoding.Default.GetString(struDevInfo.byDeviceID).TrimEnd('\0');

            try
            {
                var device = new DeviceConnection
                {
                    UserId = lUserID,
                    DeviceId = deviceId,
                    DevicePort = struDevInfo.struDevAddr.wPort,
                    DeviceIP = Encoding.Default.GetString([.. struDevInfo.struDevAddr.szIP.Select(c => (byte)c)]).TrimEnd('\0'),
                    LastHeartbeat = DateTime.UtcNow,
                    IsConnected = true,
                    RegistrationTime = DateTime.UtcNow
                };

                // 添加或更新设备
                _devices.AddOrUpdate(deviceId, device, (key, oldValue) =>
                {
                    // 如果设备重新连接，更新连接信息
                    oldValue.UserId = lUserID;
                    oldValue.LastHeartbeat = DateTime.UtcNow;
                    oldValue.IsConnected = true;
                    oldValue.ReconnectCount++;
                    return oldValue;
                });

                // 为设备创建专用队列
                await EnsureDeviceQueueExists(deviceId);

                _logger.LogInformation($"设备注册成功: {deviceId}, LoginId: {lUserID}");
                return (true, "设备注册成功", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"注册设备时发生异常: {deviceId}");
                return (false, $"注册设备异常: {ex.Message}", null);
            }
        }

        // 确保设备队列存在
        private async Task EnsureDeviceQueueExists(string deviceId)
        {
            if (!_deviceQueues.ContainsKey(deviceId))
            {
                var queueOptions = new BoundedChannelOptions(1000)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                };

                var deviceQueue = Channel.CreateBounded<DeviceMessage>(queueOptions);
                _deviceQueues.TryAdd(deviceId, deviceQueue);

                // 启动设备专用处理器
                _ = Task.Run(() => ProcessDeviceQueue(deviceId, deviceQueue.Reader));
            }
        }

        // 处理设备专用队列
        private async Task ProcessDeviceQueue(string deviceId, ChannelReader<DeviceMessage> reader)
        {
            _logger.LogInformation($"启动设备队列处理器: {deviceId}");

            try
            {
                await foreach (var message in reader.ReadAllAsync())
                {
                    await _processingLimiter.WaitAsync();
                    try
                    {
                        await ProcessDeviceMessage(message);
                    }
                    finally
                    {
                        _processingLimiter.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"设备队列处理器异常: {deviceId}");
            }
            finally
            {
                _logger.LogInformation($"设备队列处理器停止: {deviceId}");
            }
        }

        // 处理设备消息
        private async Task ProcessDeviceMessage(DeviceMessage message)
        {
            try
            {
                switch (message)
                {
                    case DeviceCommandMessage commandMessage:
                        await ProcessCommandMessage(commandMessage);
                        break;
                    case DeviceHeartbeatMessage heartbeatMessage:
                        await ProcessHeartbeatMessage(heartbeatMessage);
                        break;
                    case DeviceEventMessage eventMessage:
                        await ProcessEventMessage(eventMessage);
                        break;
                    default:
                        _logger.LogWarning($"未知消息类型: {message.GetType().Name}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"处理设备消息异常: {message.DeviceId}, MessageId: {message.MessageId}");
            }
        }

        // 处理命令消息
        private async Task ProcessCommandMessage(DeviceCommandMessage message)
        {
            if (!_devices.TryGetValue(message.DeviceId, out var device) || !device.IsConnected.Value)
            {
                message.CompletionSource?.SetResult(new DeviceCommandResult
                {
                    Success = false,
                    Message = "设备未连接",
                    ErrorCode = "DEVICE_OFFLINE"
                });
                return;
            }

            try
            {
                // 这里调用实际的设备SDK方法
                var result = await ExecuteDeviceCommand(device, message.Command, message.Parameters);
                message.CompletionSource?.SetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行设备命令异常: {message.DeviceId}, Command: {message.Command}");
                message.CompletionSource?.SetException(ex);
            }
        }

        // 处理心跳消息
        private async Task ProcessHeartbeatMessage(DeviceHeartbeatMessage message)
        {
            if (_devices.TryGetValue(message.DeviceId, out var device))
            {
                if (device.UserId == message.UserId && device.IsConnected == true)
                {
                    device.LastHeartbeat = DateTime.UtcNow;
                    device.HeartbeatCount++;
                    _logger.LogDebug($"更新设备心跳: {message.DeviceId}");
                }
            }
            await Task.CompletedTask;
        }

        // 处理事件消息
        private async Task ProcessEventMessage(DeviceEventMessage message)
        {
            _logger.LogInformation($"处理设备事件: {message.DeviceId}, Event: {message.EventType}");
            // 这里可以添加事件处理逻辑，如通知其他服务
            await Task.CompletedTask;
        }

        // 发送消息到设备队列
        public async Task<bool> SendMessageToDevice(string deviceId, DeviceMessage message)
        {
            await EnsureDeviceQueueExists(deviceId);

            if (_deviceQueues.TryGetValue(deviceId, out var queue))
            {
                return await queue.Writer.WaitToWriteAsync() && queue.Writer.TryWrite(message);
            }

            return false;
        }

        // 执行设备命令（带超时和重试）
        public async Task<DeviceCommandResult> ExecuteDeviceCommandAsync(
            string deviceId,
            string command,
            Dictionary<string, object> parameters = null,
            TimeSpan timeout = default)
        {
            if (timeout == default)
                timeout = TimeSpan.FromSeconds(30);

            var commandMessage = new DeviceCommandMessage
            {
                DeviceId = deviceId,
                Command = command,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Priority = MessagePriority.High,
                CompletionSource = new TaskCompletionSource<DeviceCommandResult>()
            };

            // 发送到设备队列
            if (!await SendMessageToDevice(deviceId, commandMessage))
            {
                return new DeviceCommandResult
                {
                    Success = false,
                    Message = "无法发送命令到设备队列",
                    ErrorCode = "QUEUE_FULL"
                };
            }

            // 等待命令执行完成
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                cts.Token.Register(() => commandMessage.CompletionSource.TrySetCanceled());

                return await commandMessage.CompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                return new DeviceCommandResult
                {
                    Success = false,
                    Message = "命令执行超时",
                    ErrorCode = "TIMEOUT"
                };
            }
        }

        // 实际执行设备命令的方法
        private async Task<DeviceCommandResult> ExecuteDeviceCommand(
            DeviceConnection device,
            string command,
            Dictionary<string, object> parameters)
        {
            // 这里实现具体的设备命令执行逻辑
            // 根据command类型调用相应的SDK方法

            switch (command.ToLower())
            {
                case "opendoor":
                    return await ExecuteOpenDoorCommand(device, parameters);
                case "getstatus":
                    return await ExecuteGetStatusCommand(device, parameters);
                // 添加更多命令处理
                default:
                    return new DeviceCommandResult
                    {
                        Success = false,
                        Message = $"不支持的命令: {command}",
                        ErrorCode = "UNSUPPORTED_COMMAND"
                    };
            }
        }

        private async Task<DeviceCommandResult> ExecuteOpenDoorCommand(
            DeviceConnection device,
            Dictionary<string, object> parameters)
        {
            // 实现开门命令
            return new DeviceCommandResult
            {
                Success = true,
                Message = "开门命令执行成功"
            };
        }

        private async Task<DeviceCommandResult> ExecuteGetStatusCommand(
            DeviceConnection device,
            Dictionary<string, object> parameters)
        {
            // 实现获取状态命令
            return new DeviceCommandResult
            {
                Success = true,
                Message = "获取状态成功",
                Data = new Dictionary<string, object>
                {
                    ["device_id"] = device?.DeviceId,
                    ["status"] = device.IsConnected.Value ? "online" : "offline",
                    ["last_heartbeat"] = device.LastHeartbeat
                }
            };
        }

        // 更新设备心跳
        public async Task<bool> UpdateDeviceHeartbeatAsync(string deviceId, int userId)
        {
            var heartbeatMessage = new DeviceHeartbeatMessage
            {
                DeviceId = deviceId,
                UserId = userId,
                Priority = MessagePriority.Low
            };

            return await SendMessageToDevice(deviceId, heartbeatMessage);
        }

        // 清理过期连接
        private void CleanupExpiredConnections(object state)
        {
            var expiredDevices = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddSeconds(-_options.HeartbeatTimeoutSeconds);

            foreach (var kvp in _devices)
            {
                var device = kvp.Value;
                if (device.LastHeartbeat < cutoffTime)
                {
                    expiredDevices.Add(kvp.Key);
                }
            }

            foreach (var deviceId in expiredDevices)
            {
                _ = Task.Run(() => DisconnectDeviceAsync(deviceId));
            }

            if (expiredDevices.Count > 0)
            {
                _logger.LogInformation($"清理了 {expiredDevices.Count} 个过期设备连接");
            }
        }

        // 设备健康检查
        private void CheckDeviceHealth(object state)
        {
            var unhealthyDevices = _devices.Values
                .Where(d => d.IsConnected == true &&
                           DateTime.UtcNow - d.LastHeartbeat > TimeSpan.FromSeconds(_options.HeartbeatTimeoutSeconds / 2))
                .ToList();

            foreach (var device in unhealthyDevices)
            {
                _logger.LogWarning($"设备心跳异常: {device.DeviceId}, 上次心跳: {device.LastHeartbeat}");
            }
        }

        // 断开设备连接
        public async Task DisconnectDeviceAsync(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                try
                {
                    device.IsConnected = false;
                    device.DisconnectTime = DateTime.UtcNow;

                    // 关闭设备队列
                    if (_deviceQueues.TryRemove(deviceId, out var queue))
                    {
                        queue.Writer.Complete();
                    }

                    _logger.LogInformation($"设备连接已断开: {deviceId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"断开设备连接时发生异常: {deviceId}");
                }
            }
        }

        // 获取在线设备列表
        public List<DeviceConnection> GetOnlineDevices()
        {
            return _devices.Values.Where(d => d.IsConnected == true).ToList();
        }

        // 获取设备统计信息
        public DeviceManagerStatistics GetStatistics()
        {
            var onlineCount = _devices.Values.Count(d => d.IsConnected == true);
            var totalQueueLength = _deviceQueues.Values.Sum(q =>
                q.Reader.CanCount ? q.Reader.Count : 0);

            return new DeviceManagerStatistics
            {
                TotalDevices = _devices.Count,
                OnlineDevices = onlineCount,
                OfflineDevices = _devices.Count - onlineCount,
                TotalQueueLength = totalQueueLength,
                ActiveQueues = _deviceQueues.Count
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("设备管理器后台服务启动");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // 这里可以添加其他后台处理逻辑
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("设备管理器后台服务正常停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备管理器后台服务异常");
            }
        }

        public override void Dispose()
        {
            // 清理所有设备连接
            foreach (var device in _devices.Values)
            {
                if (device.UserId >= 0)
                {
                    // 调用SDK清理方法
                }
            }

            // 清理队列
            foreach (var queue in _deviceQueues.Values)
            {
                queue.Writer.Complete();
            }

            _globalQueue.Writer.Complete();
            _cleanupTimer?.Dispose();
            _healthCheckTimer?.Dispose();
            _processingLimiter?.Dispose();

            base.Dispose();
            _logger.LogInformation("设备管理器已释放所有资源");
        }
    }

    // 设备连接信息（扩展版本）
    public class DeviceConnection
    {
        public string? DeviceId { get; set; }
        public string? DeviceIP { get; set; }
        public int? DevicePort { get; set; }
        public int? UserId { get; set; }
        public bool? IsConnected { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public DateTime RegistrationTime { get; set; }
        public DateTime? DisconnectTime { get; set; }
        public int ReconnectCount { get; set; }
        public long HeartbeatCount { get; set; }
        public string? FirmwareVersion { get; set; }
        public Dictionary<string, object> Properties { get; set; } = [];
    }

    // 设备管理器统计信息
    public class DeviceManagerStatistics
    {
        public int TotalDevices { get; set; }
        public int OnlineDevices { get; set; }
        public int OfflineDevices { get; set; }
        public int TotalQueueLength { get; set; }
        public int ActiveQueues { get; set; }
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
    }
}
