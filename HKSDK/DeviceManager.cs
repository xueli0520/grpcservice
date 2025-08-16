using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
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

        private readonly SubscribeEvent _subscribeEvent;

        public DeviceManager(ILogger<DeviceManager> logger, SubscribeEvent subscribeEvent)
        {
            _logger = logger;
            _devices = new ConcurrentDictionary<string, DeviceConnection>();
            _subscribeEvent = subscribeEvent;

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
                _logger.LogInformation("设备注册成功: {DeviceId}, IP: {DeviceIP}, Port: {DevicePort}, UserId: {UserId}",
                    deviceId, device.DeviceIP, device.DevicePort, lUserID);
                if (!device.Register)
                {
                    PublishDeviceEvent(device);
                    device.RegisterTime = DateTime.Now;
                }
                // 添加到设备列表
                _devices.AddOrUpdate(deviceId, device, (key, oldValue) => device);
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
                    if (!device.Register) PublishDeviceEvent(device);
                    var evt = new DeviceEvent
                    {
                        DeviceId = deviceId,
                        EventType = "HeartBeat",
                        Payload = device.DeviceIP,
                    };
                    _subscribeEvent.Publish(evt);
                    return true;
                }
            }

            _logger.LogWarning("更新心跳失败，设备不存在或UserId不匹配: {DeviceId}, UserId: {UserId}", deviceId, userId);
            return false;
        }

        public void PublishDeviceEvent(DeviceConnection device)
        {
            Task.Run(() =>
            {
                var evt = new DeviceEvent
                {
                    DeviceId = device.DeviceId,
                    EventType = "DeviceRegistered",
                    Payload = device.DeviceIP,
                };
                evt.Payload = device.DeviceIP;
                _subscribeEvent.Publish(evt);
            });
        }

        public void RegisterEvent(string deviceId) => _devices[deviceId].Register = true;
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
                    //"getstatus" => await GetDeviceStatusAsync(device),
                    //"opendoor" => await OpenDoorAsync(device, command.Parameters),
                    //"reboot" => await RebootDeviceAsync(device, command.Parameters),
                    //"synctime" => await SyncTimeAsync(device, command.Parameters),
                    //"getdeviceinfo" => await GetDeviceInfoAsync(device),
                    //"updateuserall" => await UpdateUserAllAsync(device, command.Parameters),
                    //"getuserlist" => await GetUserListAsync(device, command.Parameters),
                    //"setdoormode" => await SetDoorModeAsync(device, command.Parameters),
                    //"updatewhite" => await UpdateWhiteAsync(device, command.Parameters),
                    //"deletewhite" => await DeleteWhiteAsync(device, command.Parameters),
                    //"pagewhite" => await PageWhiteAsync(device, command.Parameters),
                    //"detailwhite" => await DetailWhiteAsync(device, command.Parameters),
                    //"updatetimezone" => await UpdateTimezoneAsync(device, command.Parameters),
                    //"querytimezone" => await QueryTimezoneAsync(device, command.Parameters),
                    //"setdoortemplate" => await SetDoorTemplateAsync(device, command.Parameters),
                    //"syncdeviceparameter" => await SyncDeviceParameterAsync(device, command.Parameters),
                    //"getwhiteusertotal" => await GetWhiteUserTotalAsync(device),
                    //"getversion" => await GetVersionAsync(device),
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

        // 统一的 ISAPI 执行器
        public Task<T> ExecuteIsapi<T>(string deviceId, string url, string method, string? inXml, Func<bool, string, T> map) where T : class
        {
            if (!_devices.TryGetValue(deviceId, out var device) || device.IsConnected != true)
            {
                return Task.FromResult(map(false, "设备未连接") as T);
            }
            HCOTAPCMS.OTAP_CMS_ISAPI_PT_PARAM struParam = new();
            struParam.Init();

            //输入ISAPI协议命令
            uint dwRequestUrlLen = (uint)url.Length;
            struParam.pRequestUrl = Marshal.StringToHGlobalAnsi(url);
            struParam.dwRequestUrlLen = dwRequestUrlLen;
            _logger.LogInformation("透传URL: {Url}", url);

            //输入XML/JSON报文, GET命令输入报文为空
            if (!string.IsNullOrEmpty(inXml) || inXml != null)
            {
                byte[] byInputParam = Encoding.UTF8.GetBytes(inXml!);
                int iXMLInputLen = byInputParam.Length;

                struParam.pInBuffer = Marshal.AllocHGlobal(iXMLInputLen);
                Marshal.Copy(byInputParam, 0, struParam.pInBuffer, iXMLInputLen);
                struParam.dwInSize = (uint)byInputParam.Length;

                _logger.LogInformation("透传报文: {InXml}", inXml);
            }

            struParam.pOutBuffer = Marshal.AllocHGlobal(20 * 1024);    //输出缓冲区，如果接口调用失败提示错误码43，需要增大输出缓冲区
            struParam.dwOutSize = 20 * 1024;

            if (!HCOTAPCMS.OTAP_CMS_ISAPIPassThrough((int)device.UserId, ref struParam))
            {
                _logger.LogError($"{deviceId},{url} OTAP_CMS_ISAPIPassThrough failed, error:" + HCOTAPCMS.OTAP_CMS_GetLastError());
                return Task.FromResult(map(false, $"指令下发失败{HCOTAPCMS.OTAP_CMS_GetLastError()}"));
            }
            // 读取输出
            var returned = struParam.dwReturnedLen > 0 ? (int)struParam.dwReturnedLen : (int)struParam.dwOutSize;
            var outBytes = new byte[returned];
            Marshal.Copy(struParam.pOutBuffer, outBytes, 0, outBytes.Length);
            var outText = Encoding.UTF8.GetString(outBytes).TrimEnd('\0');

            // 释放
            Marshal.FreeHGlobal(struParam.pRequestUrl);
            Marshal.FreeHGlobal(struParam.pOutBuffer);
            Marshal.FreeHGlobal(struParam.pCondBuffer);
            if (inXml != null) Marshal.FreeHGlobal(struParam.pInBuffer);

            _logger.LogInformation("返回结果: {OutText}", outText);
            return Task.FromResult(map(true, outText));
        }
        public Task<(bool Success, string Message)> Cms_SetConfigDevAsync(string deviceId,
    HCOTAPCMS.OTAP_CMS_CONFIG_DEV_ENUM enumMsg, string sDomain, string sIdentifier, string inputData)
        {
            if (!_devices.TryGetValue(deviceId, out var device) || device.IsConnected != true)
            {
                return Task.FromResult((false, "设备未连接"));
            }
            try
            {
                HCOTAPCMS.OTAP_CMS_CONFIG_DEV_PARAM struConfigParam = new HCOTAPCMS.OTAP_CMS_CONFIG_DEV_PARAM();
                struConfigParam.Init();
                //子设备ID,设备本身固定为global
                string sChildID = "global";
                sChildID.CopyTo(0, struConfigParam.szChildID, 0, sChildID.Length);
                //设备本地资源标识,设备本身固定为0
                string sLocalIndex = "0";
                sLocalIndex.CopyTo(0, struConfigParam.szLocalIndex, 0, sLocalIndex.Length);
                //设备资源类型,设备本身固定为global
                string sResourceType = "global";
                sResourceType.CopyTo(0, struConfigParam.szResourceType, 0, sResourceType.Length);
                //功能领域，不同功能对应不同领域，详见OTAP协议文档
                sDomain.CopyTo(0, struConfigParam.szDomain, 0, sDomain.Length);
                //功能标识/属性标识，不同功能对应不同领域，详见OTAP协议文档
                sIdentifier.CopyTo(0, struConfigParam.szIdentifier, 0, sIdentifier.Length);

                if (!string.IsNullOrEmpty(inputData))
                {
                    byte[] byInputParam = Encoding.UTF8.GetBytes(inputData);
                    int iXMLInputLen = byInputParam.Length;
                    struConfigParam.pInBuf = Marshal.AllocHGlobal(iXMLInputLen);
                    Marshal.Copy(byInputParam, 0, struConfigParam.pInBuf, iXMLInputLen);
                    struConfigParam.dwInBufSize = (uint)byInputParam.Length;
                }

                struConfigParam.pOutBuf = Marshal.AllocHGlobal(20 * 1024);    //输出缓冲区，如果接口调用失败提示错误码43，需要增大输出缓冲区
                struConfigParam.dwOutBufSize = 20 * 1024;

                bool success = HCOTAPCMS.OTAP_CMS_ConfigDev((int)device.UserId, enumMsg, ref struConfigParam);
                string outText;

                if (success)
                {
                    var returned = (int)struConfigParam.dwOutBufSize;
                    var outBufferPtr = struConfigParam.pOutBuf;
                    var outBytes = new byte[returned];
                    Marshal.Copy(outBufferPtr, outBytes, 0, outBytes.Length);
                    outText = Encoding.UTF8.GetString(outBytes).TrimEnd('\0');
                    _logger.LogInformation($"调用成功:{outText}");
                }
                else
                {
                    outText = $"调用失败: {HCOTAPCMS.OTAP_CMS_GetLastError()}";
                    _logger.LogError(outText);
                }

                // 释放内存
                if (struConfigParam.pInBuf != IntPtr.Zero)
                    Marshal.FreeHGlobal(struConfigParam.pInBuf);
                if (struConfigParam.pOutBuf != IntPtr.Zero)
                    Marshal.FreeHGlobal(struConfigParam.pOutBuf);

                return Task.FromResult((success, outText));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cms_SetConfigDevAsync异常");
                return Task.FromResult((false, ex.Message));
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
        public bool Register { get; set; }
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