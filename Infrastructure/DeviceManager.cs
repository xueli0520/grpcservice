using Grpc.Core;
using GrpcService.HKSDK;
using GrpcService.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace GrpcService.Infrastructure
{
    public class DeviceManager(
        ILogger<DeviceManager> logger,
        DeviceLoggerService deviceLogger, SubscribeEvent subscribeEvent, TenantConcurrencyManager tenantConcurrency,
        RedisService redis)
    {
        private readonly ILogger<DeviceManager> _logger = logger;
        private readonly DeviceLoggerService _deviceLogger = deviceLogger;
        private readonly RedisService _redis = redis;

        private readonly ConcurrentDictionary<string, DeviceConnection> _devices = new();
        private readonly SubscribeEvent _subscribeEvent = subscribeEvent;
        private readonly TenantConcurrencyManager _tenantConcurrency = tenantConcurrency;

        public async Task<(bool Success, string Message, string DeviceId)> RegisterDevice(
            int lUserID, HCOTAPCMS.OTAP_CMS_DEV_REG_INFO struDevInfo)
        {
            string deviceId = Encoding.Default.GetString(struDevInfo.byDeviceID).TrimEnd('\0');
            try
            {
                var device = new DeviceConnection(deviceId, new string(struDevInfo.struDevAddr.szIP).TrimEnd('\0'), struDevInfo.struDevAddr.wPort, lUserID, _deviceLogger);
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
                _logger.LogError(ex, "设备注册失败: {DeviceId}", struDevInfo.byDeviceID);
                return (false, ex.Message, deviceId);
            }
            return (true, "设备注册成功", deviceId);
        }

        // 统一的事件推送方法
        public async Task PublishDeviceEvent(DeviceEvent deviceEvent)
        {
            var streamKey = "device:events:stream";
            var eventData = JsonSerializer.Serialize(deviceEvent);

            await _redis.StreamCreateConsumerGroupAsync(streamKey, "data", eventData);
            _logger.LogDebug("设备事件已发布到Stream: {EventType}", deviceEvent.EventType);
        }
        public void RegisterEvent(string deviceId) => _devices[deviceId].Register = true;

        public async Task<bool> UpdateDeviceHeartbeat(string deviceId, int userId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                if (device?.UserId == userId && device.IsConnected == true)
                {
                    device.LastHeartbeat = DateTime.Now;
                    _logger.LogDebug("更新设备心跳: {DeviceId}", deviceId);
                    if (!device.Register)
                        await PublishDeviceEvent(new DeviceEvent { EventType = "DeviceRegistered", DeviceId = deviceId, Payload = JsonSerializer.Serialize(device) });
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
                    count: 100,
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
            try
            {
                // 从Stream条目中获取事件数据
                var eventDataField = entry.Values.FirstOrDefault(x => x.Name == "data");
                if (eventDataField.Value.HasValue)
                {
                    var deviceEvent = JsonSerializer.Deserialize<DeviceEvent>(eventDataField.Value!);
                    if (deviceEvent != null)
                    {
                        // 向客户端发送事件
                        await responseStream.WriteAsync(deviceEvent);

                        // 确认消息已处理
                        await _redis.AcknowledgeStreamMessage(streamKey, consumerGroup, entry.Id!);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "反序列化设备事件失败，消息ID: {MessageId}", entry.Id);
                // 确认错误消息，避免重复处理
                await _redis.AcknowledgeStreamMessage(streamKey, consumerGroup, entry.Id!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理Stream条目失败，消息ID: {MessageId}", entry.Id);
                // 不确认消息，允许重试
            }
        }


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
                HCOTAPCMS.OTAP_CMS_CONFIG_DEV_PARAM struConfigParam = new();
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
