using GrpcService.Models;
using Microsoft.Graph.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace GrpcService.Infrastructure;
public class TenantAwareDeviceManager(
    ILogger<TenantAwareDeviceManager> logger,
    IConnectionMultiplexer redis)
{
    private readonly ILogger<TenantAwareDeviceManager> _logger = logger;
    private readonly IConnectionMultiplexer _redis = redis;

    // 更新白名单（添加或更新）
    public async Task<bool> UpdateWhiteAsync(string tenantId, string deviceId, string cardNo, string personName)
    {
        try
        {
            // 获取设备信息
            var device = await GetDeviceByTenantAsync(tenantId, deviceId);
            if (device == null)
            {
                _logger.LogWarning("设备 {DeviceId} 在租户 {TenantId} 中未找到", deviceId, tenantId);
                return false;
            }

            // 设备操作：更新白名单（假设有 AddWhiteUserAsync 方法）
            var success = await AddWhiteUserAsync(device, cardNo, personName);

            if (success)
            {
                // 发布事件到 Redis 以供客户端接收
                await PublishWhiteListEventAsync(tenantId, deviceId, "UpdateWhite", cardNo, personName);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新白名单失败 TenantId={TenantId}, DeviceId={DeviceId}", tenantId, deviceId);
            return false;
        }
    }

    // 删除白名单
    public async Task<bool> DeleteWhiteAsync(string tenantId, string deviceId, string cardNo)
    {
        try
        {
            // 获取设备信息
            var device = await GetDeviceByTenantAsync(tenantId, deviceId);
            if (device == null)
            {
                _logger.LogWarning("设备 {DeviceId} 在租户 {TenantId} 中未找到", deviceId, tenantId);
                return false;
            }

            // 设备操作：删除白名单
            var success = await RemoveWhiteUserAsync(device, cardNo);

            if (success)
            {
                // 发布事件到 Redis 以供客户端接收
                await PublishWhiteListEventAsync(tenantId, deviceId, "DeleteWhite", cardNo, null);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除白名单失败 TenantId={TenantId}, DeviceId={DeviceId}", tenantId, deviceId);
            return false;
        }
    }

    // 分页查询白名单
    public async Task<List<string>> PageWhiteAsync(string tenantId, string deviceId, int pageIndex, int pageSize)
    {
        try
        {
            // 获取设备信息
            var device = await GetDeviceByTenantAsync(tenantId, deviceId);
            if (device == null)
            {
                _logger.LogWarning("设备 {DeviceId} 在租户 {TenantId} 中未找到", deviceId, tenantId);
                return [];
            }

            // 设备操作：查询白名单（假设有 GetWhiteListAsync 方法）
            var users = await GetWhiteListAsync(device, pageIndex, pageSize);

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分页查询白名单失败 TenantId={TenantId}, DeviceId={DeviceId}", tenantId, deviceId);
            return new List<string>();
        }
    }

    // 获取设备信息（根据租户与设备 ID）
    private async Task<DeviceConnection> GetDeviceByTenantAsync(string tenantId, string deviceId)
    {
        // 获取设备信息的业务逻辑（从数据库、Redis 或 API）
        var db = _redis.GetDatabase();
        var deviceJson = await db.StringGetAsync($"device:{tenantId}:{deviceId}");
        if (string.IsNullOrEmpty(deviceJson))
        {
            _logger.LogWarning("设备 {DeviceId} 在租户 {TenantId} 中不存在", deviceId, tenantId);
            return null;
        }

        return JsonSerializer.Deserialize<DeviceConnection>(deviceJson!)!;
    }

    // 更新白名单用户
    private static async Task<bool> AddWhiteUserAsync(DeviceConnection device, string cardNo, string personName)
    {
        // 调用设备 SDK 执行更新白名单操作
        // 假设通过设备 SDK 成功返回 true，失败返回 false
        return await Task.FromResult(true);
    }

    // 删除白名单用户
    private static async Task<bool> RemoveWhiteUserAsync(DeviceConnection device, string cardNo)
    {
        // 调用设备 SDK 执行删除白名单操作
        return await Task.FromResult(true);
    }

    // 查询白名单用户
    private static async Task<List<string>> GetWhiteListAsync(DeviceConnection device, int pageIndex, int pageSize)
    {
        // 调用设备 SDK 执行查询白名单操作
        return await Task.FromResult(new List<string> { "User1", "User2" });
    }

    // 发布白名单事件到 Redis Pub/Sub
    private async Task PublishWhiteListEventAsync(string tenantId, string deviceId, string action, string cardNo, string personName)
    {
        var eventMessage = new
        {
            EventType = action,
            DeviceId = deviceId,
            TenantId = tenantId,
            CardNo = cardNo,
            PersonName = personName,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        var db = _redis.GetDatabase();
        await db.PublishAsync($"device:events:{tenantId}", JsonSerializer.Serialize(eventMessage));
        _logger.LogInformation("已发布事件 {EventType} 到 Redis，设备 {DeviceId}", action, deviceId);
    }
}
