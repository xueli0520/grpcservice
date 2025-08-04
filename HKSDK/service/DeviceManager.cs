using System.Collections.Concurrent;
using System.Text;

namespace GrpcService.HKSDK.service
{
    public class DeviceManager : BackgroundService
    {
        private readonly ILogger<DeviceManager> _logger;
        private readonly ConcurrentDictionary<string, DeviceConnection> _devices;
        private readonly Timer _heartbeatTimer;
        private readonly int _heartbeatTimeoutSeconds = 120; // 心跳超时时间
        private readonly int _heartbeatCheckIntervalSeconds = 60; // 心跳检查间隔

        public DeviceManager(ILogger<DeviceManager> logger)
        {
            _logger = logger;
            _devices = new ConcurrentDictionary<string, DeviceConnection>();
            // 启动心跳检测定时器
            _heartbeatTimer = new Timer(CheckDeviceHeartbeat, null,
                TimeSpan.FromSeconds(_heartbeatCheckIntervalSeconds),
                TimeSpan.FromSeconds(_heartbeatCheckIntervalSeconds));

            _logger.LogInformation("设备管理器初始化完成");
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
                if (_devices.ContainsKey(deviceId))
                {
                    var existingDevice = _devices[deviceId];
                    if (!existingDevice.IsConnected.Value)
                    {
                        // 尝试重新连接
                        await DisconnectDeviceAsync(deviceId);
                    }
                    else
                    {
                        existingDevice.LastHeartbeat = DateTime.Now;
                        return (true, "设备已连接", existingDevice.DeviceId);
                    }
                }

                // 创建新的设备连接
                var device = new DeviceConnection
                {
                    UserId = lUserID,
                    DeviceId = deviceId,
                    DevicePort = struDevInfo.struDevAddr.wPort,
                    DeviceIP = struDevInfo.struDevAddr.szIP?.ToString(),
                    LastHeartbeat = DateTime.Now,
                    IsConnected = true,
                };

                // 添加到设备列表
                _devices.AddOrUpdate(deviceId, device, (key, oldValue) => device);

                _logger.LogInformation($"设备注册成功: {deviceId}, DeviceId: {deviceId}");
                return (true, "设备注册成功", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"注册设备时发生异常: {deviceId}");
                return (false, $"注册设备异常: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 更新设备心跳
        /// </summary>
        public bool UpdateDeviceHeartbeat(string deviceId, string sessionId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                if (device.SessionId == sessionId && device.IsConnected)
                {
                    device.LastHeartbeat = DateTime.Now;
                    _logger.LogDebug($"更新设备心跳: {deviceId}");
                    return true;
                }
            }

            _logger.LogWarning($"更新心跳失败，设备不存在或SessionId不匹配: {deviceId}");
            return false;
        }

        /// <summary>
        /// 执行设备命令
        /// </summary>
        public async Task<(bool Success, string Message, Dictionary<string, string> ResultData)>
            ExecuteDeviceCommandAsync(string deviceId, string commandType, Dictionary<string, string> parameters)
        {
            if (!_devices.TryGetValue(deviceId, out var device) || !device.IsConnected)
            {
                return (false, "设备未连接", null);
            }

            try
            {
                // 根据命令类型执行相应操作
                switch (commandType.ToLower())
                {
                    case "getstatus":
                        return await GetDeviceStatusAsync(device);
                    case "ptz":
                        return await ExecutePTZCommandAsync(device, parameters);
                    case "preset":
                        return await ExecutePresetCommandAsync(device, parameters);
                    default:
                        return (false, $"不支持的命令类型: {commandType}", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行设备命令异常: {deviceId}, 命令: {commandType}");
                return (false, $"执行命令异常: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 获取设备状态
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, string> ResultData)>
            GetDeviceStatusAsync(DeviceConnection device)
        {
            var resultData = new Dictionary<string, string>
            {
                ["device_id"] = device.DeviceId,
                ["status"] = device.IsConnected ? "online" : "offline",
                ["last_heartbeat"] = device.LastHeartbeat.ToString("yyyy-MM-dd HH:mm:ss"),
                ["login_id"] = device.LoginId.ToString()
            };

            return (true, "获取状态成功", resultData);
        }

        /// <summary>
        /// 执行PTZ命令
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, string> ResultData)>
            ExecutePTZCommandAsync(DeviceConnection device, Dictionary<string, string> parameters)
        {
            // 这里实现具体的PTZ控制逻辑
            // 示例实现
            var resultData = new Dictionary<string, string>
            {
                ["command"] = "ptz",
                ["status"] = "executed"
            };

            return (true, "PTZ命令执行成功", resultData);
        }

        /// <summary>
        /// 执行预置点命令
        /// </summary>
        private async Task<(bool Success, string Message, Dictionary<string, string> ResultData)>
            ExecutePresetCommandAsync(DeviceConnection device, Dictionary<string, string> parameters)
        {
            // 这里实现具体的预置点控制逻辑
            var resultData = new Dictionary<string, string>
            {
                ["command"] = "preset",
                ["status"] = "executed"
            };

            return (true, "预置点命令执行成功", resultData);
        }

        /// <summary>
        /// 获取在线设备列表
        /// </summary>
        public List<DeviceConnection> GetOnlineDevices()
        {
            return _devices.Values.Where(d => d.IsConnected).ToList();
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
                var timeSinceLastHeartbeat = now - device.LastHeartbeat;

                if (timeSinceLastHeartbeat?.TotalSeconds <= _heartbeatTimeoutSeconds)
                {
                    continue;
                }
                _logger.LogWarning($"设备心跳超时，准备断开连接: {device.DeviceId}");
                devicesToRemove.Add(kvp.Key);
            }

            // 移除超时设备
            foreach (var deviceId in devicesToRemove)
            {
                _ = Task.Run(() => DisconnectDeviceAsync(deviceId));
            }
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        private async Task DisconnectDeviceAsync(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                try
                {
                    if (device.LoginId >= 0)
                    {
                        HikvisionDeviceWrapper.NET_DVR_Logout_V30(device.LoginId);
                    }
                    device.IsConnected = false;
                    _logger.LogInformation($"设备连接已断开: {deviceId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"断开设备连接时发生异常: {deviceId}");
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override void Dispose()
        {
            // 清理所有设备连接
            foreach (var device in _devices.Values)
            {
                if (device.LoginId >= 0)
                {
                    HikvisionDeviceWrapper.NET_DVR_Logout_V30(device.LoginId);
                }
            }

            // 清理海康NET DVR SDK
            HikvisionDeviceWrapper.NET_DVR_Cleanup();

            // 清理OTAP CMS SDK
            HikvisionDeviceWrapper.OTAP_CMS_Cleanup();

            _heartbeatTimer?.Dispose();
            base.Dispose();

            _logger.LogInformation("设备管理器已释放所有资源");
        }
    }
}
