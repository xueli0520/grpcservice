using Grpc.Core;
using GrpcService.Api;
using GrpcService.HKSDK;
using GrpcService.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace GrpcService.Infrastructure
{
    public class DeviceManager(
        ILogger<DeviceManager> logger,
        DeviceLoggerService deviceLogger, SubscribeEvent subscribeEvent,
        RedisService? redis = null)
    {
        private readonly ILogger<DeviceManager> _logger = logger;
        private readonly DeviceLoggerService _deviceLogger = deviceLogger;
        private readonly RedisService? _redis = redis;

        private readonly ConcurrentDictionary<string, DeviceConnection> _devices = new();
        private readonly SubscribeEvent _subscribeEvent = subscribeEvent;
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
                    await PublishEventAsync(new EventMessage
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

            await _redis.StreamAddAsync(streamKey, "data", eventData);
            _logger.LogDebug("设备事件已发布到Stream: {EventType}", deviceEvent.EventType);
        }

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
                var pendingResults = await _redisDatabase.StreamReadGroupAsync(
                    streamKey,
                    consumerGroup,
                    consumerName,
                    "0", // 从最早的未确认消息开始
                    count: 100,
                    noAck: false);

                foreach (var result in pendingResults)
                {
                    foreach (var entry in result.Values)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        await ProcessStreamEntry(entry, responseStream, streamKey, consumerGroup);
                    }
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
                    var deviceEvent = JsonSerializer.Deserialize<DeviceEvent>(eventDataField.Value);
                    if (deviceEvent != null)
                    {
                        // 向客户端发送事件
                        await responseStream.WriteAsync(deviceEvent);

                        // 确认消息已处理
                        await _redisDatabase.StreamAcknowledgeAsync(streamKey, consumerGroup, entry.Id);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "反序列化设备事件失败，消息ID: {MessageId}", entry.Id);
                // 确认错误消息，避免重复处理
                await _redisDatabase.StreamAcknowledgeAsync(streamKey, consumerGroup, entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理Stream条目失败，消息ID: {MessageId}", entry.Id);
                // 不确认消息，允许重试
            }
        }

    }
}
