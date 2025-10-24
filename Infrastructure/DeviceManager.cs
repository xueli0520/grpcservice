using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcService.HKSDK;
using GrpcService.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using static GrpcService.HKSDK.HCISUPCMS;

namespace GrpcService.Infrastructure
{
    public class DeviceManager(
        ILogger<DeviceManager> logger,
        DeviceLoggerService deviceLogger, TenantConcurrencyManager tenantConcurrency,
        RedisService redis)
    {
        private readonly ILogger<DeviceManager> _logger = logger;
        private readonly DeviceLoggerService _deviceLogger = deviceLogger;
        private readonly RedisService _redis = redis;

        private readonly ConcurrentDictionary<string, DeviceConnection> _devices = new();
        private readonly TenantConcurrencyManager _tenantConcurrency = tenantConcurrency;

        public async Task<(bool Success, string Message, string DeviceId)> RegisterDevice(
            int lUserID, NET_EHOME_DEV_REG_INFO_V12 struDevInfo)
        {
            string deviceId = Encoding.Default.GetString(struDevInfo.struRegInfo.byDeviceID).TrimEnd('\0');
            try
            {
                var device = new DeviceConnection(deviceId, new string(struDevInfo.struRegInfo.struDevAdd.szIP).TrimEnd('\0'), struDevInfo.struRegInfo.struDevAdd.wPort, lUserID, _deviceLogger);
                RemoveDevice(deviceId);
                if (_devices.TryAdd(deviceId, device))
                {
                    _logger.LogInformation("设备注册成功: {DeviceId}", deviceId);
                    await PublishDeviceEvent(new DeviceEvent
                    {
                        EventType = "DeviceRegistered",
                        DeviceId = deviceId,
                        Payload = JsonSerializer.Serialize(device)
                    });
                    device.RegisterTime = DateTime.Now;
                    _deviceLogger.LogDeviceInfo(deviceId, "设备注册成功");
                    _ = UpdateDeviceStatusAsync(deviceId, "register");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备注册失败: {DeviceId}", struDevInfo.struRegInfo.byDeviceID);
                return (false, ex.Message, deviceId);
            }
            return (true, "设备注册成功", deviceId);
        }

        // 统一的事件推送方法
        public async Task PublishDeviceEvent(DeviceEvent deviceEvent)
        {
            await _redis.PublishStreamAsync(
                   [
                new NameValueEntry("EventType", deviceEvent.EventType),
                new NameValueEntry("Payload", JsonSerializer.Serialize(deviceEvent))
                   ]
               );
        }
        public void RegisterEvent(string deviceId) => _devices[deviceId].Register = true;

        public void DeleteDeviceEvent(string deviceId) => _devices[deviceId].Register = false;

        public async Task<bool> UpdateDeviceHeartbeat(string deviceId, int userId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                if (device?.DeviceId == deviceId && device.IsConnected == true)
                {
                    device.LastHeartbeat = DateTime.Now;
                    _logger.LogDebug("更新设备心跳: {DeviceId}", deviceId);
                    if (!device.Register)
                        await PublishDeviceEvent(new DeviceEvent
                        {
                            EventType = "DeviceRegistered",
                            DeviceId = deviceId,
                            Payload = JsonSerializer.Serialize(device)
                        });
                    else
                    {
                        var evt = new DeviceEvent
                        {
                            DeviceId = deviceId,
                            EventType = "HeartBeat",
                            Payload = device.DeviceIP,
                        };
                        await PublishDeviceEvent(evt);
                    }
                    return true;
                }
            }
            _logger.LogWarning("更新心跳失败，设备不存在或UserId不匹配: {DeviceId}, UserId: {UserId}", deviceId, userId);
            return false;
        }

        public bool RemoveDevice(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out _))
            {
                _logger.LogInformation("设备连接已断开: {DeviceId}", deviceId);
                _deviceLogger.LogDeviceInfo(deviceId, "设备连接已断开");
                _ = UpdateDeviceStatusAsync(deviceId, "disconnected");
                return true;
            }
            return false;
        }

        public DeviceConnection? GetDevice(string deviceId)
        {
            _devices.TryGetValue(deviceId, out var device);
            return device;
        }

        private async Task UpdateDeviceStatusAsync(string deviceId, string status)
        {
            if (_redis == null) return;

            try
            {
                var key = $"hk:device:{deviceId}:status";
                await _redis.SetStringAsync(key, status, TimeSpan.FromMinutes(30));
                _logger.LogInformation("设备状态写入 Redis: {DeviceId} -> {Status}", deviceId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入 Redis 设备状态失败: {DeviceId}", deviceId);
            }
        }

        public async Task ProcessPendingMessages(string streamKey, string consumerGroup, string consumerName, IServerStreamWriter<DeviceEvent> responseStream, CancellationToken cancellationToken)
        {
            try
            {
                // 读取未确认的消息
                var pendingResults = await _redis.StreamReadGroupAsync(
                    streamKey,
                    consumerGroup,
                    consumerName,
                    "0", // 从最早的未确认消息开始
                    count: 50,
                    noAck: false);

                foreach (var result in pendingResults)
                {
                    //foreach (var entry in result.Values)
                    //{
                    //    if (cancellationToken.IsCancellationRequested) return;
                    //    await ProcessStreamEntry(entry, responseStream, streamKey, consumerGroup);
                    //}
                    if (cancellationToken.IsCancellationRequested) return;
                    await ProcessStreamEntry(result, responseStream, streamKey, consumerGroup);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理待确认消息失败");
            }
        }

        public async Task ProcessStreamEntry(StreamEntry entry, IServerStreamWriter<DeviceEvent> responseStream, string streamKey, string consumerGroup)
        {
            if (entry.Values.Length == 0)
                return;

            try
            {
                var eventType = entry.Values.FirstOrDefault(v => v.Name == "EventType").Value;
                var payload = entry.Values.FirstOrDefault(v => v.Name == "Payload").Value;

                if (!string.IsNullOrEmpty(payload))
                {
                    var deviceEvent = JsonSerializer.Deserialize<DeviceEvent>(payload!);
                    if (deviceEvent != null)
                    {
                        await responseStream.WriteAsync(deviceEvent);
                    }
                }

                // 处理完成后 ACK
                await _redis.AcknowledgeStreamMessage(streamKey, consumerGroup, entry.Id!);
                _logger.LogDebug("消息 {Id} 已确认 (ACK)，类型 {EventType}", entry.Id, eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息 {Id} 失败", entry.Id);
                // ❌ 不 ACK，留在 Pending 里，之后可以重试
            }
        }


        public Task<T> ExecuteIsapi<T>(string deviceId, string url, string method, string? inXml, Func<bool, string, T> map) where T : class
        {
            if (!_devices.TryGetValue(deviceId, out var device) || device.IsConnected != true)
            {
                return Task.FromResult(map(false, "设备未连接") as T);
            }
            NET_EHOME_PTXML_PARAM struParam = new();
            struParam.Init();

            //输入ISAPI协议命令
            uint dwRequestUrlLen = (uint)url.Length;
            struParam.pRequestUrl = Marshal.StringToHGlobalAnsi(url);
            struParam.dwRequestUrlLen = dwRequestUrlLen;
            _logger.LogInformation("透传URL: {Url}", url);
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

            if (!NET_ECMS_ISAPIPassThrough(device.UserId, ref struParam))
            {
                _logger.LogError($"{deviceId},{url} NET_ECMS_ISAPIPassThrough failed, error:" + NET_ECMS_GetLastError());
                return Task.FromResult(map(false, $"指令下发失败{NET_ECMS_GetLastError()}"));
            }
            // 读取输出
            uint iXMSize = struParam.dwOutSize;
            byte[] managedArray = new byte[iXMSize];
            Marshal.Copy(struParam.pOutBuffer, managedArray, 0, (int)iXMSize);
            string strOutBuffer = Encoding.UTF8.GetString(managedArray);

            // 释放
            Marshal.FreeHGlobal(struParam.pRequestUrl);
            Marshal.FreeHGlobal(struParam.pOutBuffer);
            Marshal.FreeHGlobal(struParam.pCondBuffer);
            if (inXml != null) Marshal.FreeHGlobal(struParam.pInBuffer);

            _logger.LogInformation("返回结果: {OutText}", strOutBuffer);
            return Task.FromResult(map(true, strOutBuffer));
        }
        public Task<(bool Success, string Message)> Cms_SetConfigDevAsync(string deviceId,
   string sDomain, string sIdentifier, string inputData)
        {
            if (!_devices.TryGetValue(deviceId, out var device) || device.IsConnected != true)
            {
                return Task.FromResult((false, "设备未连接"));
            }
            try
            {
                NET_EHOME_VERSION_INFO struDevInfo = new();
                struDevInfo.Init();
                NET_EHOME_CONFIG struCfg = new();
                struCfg.Init();

                struDevInfo.dwSize = Marshal.SizeOf(struDevInfo);

                IntPtr ptrDevInfo = Marshal.AllocHGlobal(struDevInfo.dwSize);
                Marshal.StructureToPtr(struDevInfo, ptrDevInfo, false);

                struCfg.pOutBuf = ptrDevInfo;
                struCfg.dwOutSize = (uint)struDevInfo.dwSize;
                uint dwConfigSize = (uint)Marshal.SizeOf(struCfg);
                IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(struCfg));
                bool success = NET_ECMS_GetDevConfig((int)device.UserId, NET_EHOME_GET_DEVICE_INFO, ref struCfg, dwConfigSize);
                string outText;
                if (success)
                {
                    struDevInfo = (NET_EHOME_VERSION_INFO)Marshal.PtrToStructure(ptrDevInfo, typeof(NET_EHOME_VERSION_INFO));
                    outText = struDevInfo.ToString();
                    _logger.LogInformation($"调用成功:{struDevInfo}");
                }
                else
                {
                    outText = $"调用失败: {NET_ECMS_GetLastError()}";
                    _logger.LogError(outText);
                }
                return Task.FromResult((success, outText));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cms_SetConfigDevAsync异常");
                return Task.FromResult((false, ex.Message));
            }
        }

        /// <summary>
        /// Helper: 在调用 ExecuteIsapi 前后进行租户并发控制 & 可选 Redis 写状态
        /// 说明：tenantConcurrency 是通过 deviceId 隐式解析租户（如果 tenant map 可用）
        /// </summary>
        public async Task<T> ExecuteIsapiWithConcurrency<T>(string deviceId, Func<Task<T>> execFunc, string? redisKeyOnStart = null, string? redisKeyOnComplete = null)
            where T : class
        {
            using (await _tenantConcurrency.AcquireAsync(deviceId))
            {
                if (!string.IsNullOrEmpty(redisKeyOnStart) && _redis != null)
                {
                    try { await _redis.SetStringAsync(redisKeyOnStart, "started", TimeSpan.FromMinutes(10)); }
                    catch
                    {
                        _logger.LogError("忽略 Redis 写入错误");
                    }
                }

                try
                {
                    var result = await execFunc();

                    if (!string.IsNullOrEmpty(redisKeyOnComplete) && _redis != null)
                    {
                        try
                        {
                            await _redis.SetStringAsync(redisKeyOnComplete, "success", TimeSpan.FromMinutes(10));
                        }
                        catch
                        {
                            _logger.LogError("忽略 Redis 写入错误");
                        }
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(redisKeyOnComplete) && _redis != null)
                    {
                        try
                        {
                            await _redis.SetStringAsync(redisKeyOnComplete, "error:" + ex.Message, TimeSpan.FromMinutes(10));
                        }
                        catch
                        {
                            _logger.LogError("忽略 Redis 写入错误");
                        }
                    }
                    throw;
                }
            }
        }
    }
}
