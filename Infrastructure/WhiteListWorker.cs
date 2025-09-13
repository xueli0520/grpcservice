using GrpcService.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace GrpcService.Infrastructure;
public class WhiteListWorker(
    ILogger<WhiteListWorker> logger,
    IConnectionMultiplexer redis,
    TenantAwareDeviceManager deviceManager) : BackgroundService
{
    private readonly ILogger<WhiteListWorker> _logger = logger;
    private readonly IConnectionMultiplexer _redis = redis;
    private readonly TenantAwareDeviceManager _deviceManager = deviceManager;

    private const string QueueKey = "whitelist:queue"; // 正常任务队列
    private const string FailedQueueKey = "whitelist:failed"; // 死信队列

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        _logger.LogInformation("WhiteListWorker started, listening Redis queue {QueueKey}", QueueKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var redisValue = await db.ListLeftPopAsync(QueueKey);
                if (redisValue.IsNullOrEmpty)
                {
                    await Task.Delay(500, stoppingToken); // 无任务时等待
                    continue;
                }

                var task = JsonSerializer.Deserialize<WhiteListTask>(redisValue);
                if (task == null)
                {
                    _logger.LogWarning("Invalid whitelist task: {Value}", redisValue);
                    continue;
                }

                await ProcessTaskAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing whitelist task");
            }
        }
    }

    private async Task ProcessTaskAsync(WhiteListTask task)
    {
        try
        {
            switch (task.Type)
            {
                case "UpdateWhite":
                    var updateSuccess = await _deviceManager.UpdateWhiteAsync(task.TenantId, task.DeviceId, task.CardNo, task.PersonName);
                    if (updateSuccess)
                    {
                        await PublishWhiteListEventAsync(task, "UpdateWhiteResult", success: true);
                    }
                    else
                    {
                        await PublishWhiteListEventAsync(task, "UpdateWhiteResult", success: false);
                        await RetryTaskAsync(task); // 失败推送到死信队列
                    }
                    break;

                case "DeleteWhite":
                    var deleteSuccess = await _deviceManager.DeleteWhiteAsync(task.TenantId, task.DeviceId, task.CardNo);
                    if (deleteSuccess)
                    {
                        await PublishWhiteListEventAsync(task, "DeleteWhiteResult", success: true);
                    }
                    else
                    {
                        await PublishWhiteListEventAsync(task, "DeleteWhiteResult", success: false);
                        await RetryTaskAsync(task); // 失败推送到死信队列
                    }
                    break;

                case "PageWhite":
                    var whiteList = await _deviceManager.PageWhiteAsync(task.TenantId, task.DeviceId, task.PageIndex, task.PageSize);
                    await PublishWhiteListEventAsync(task, "PageWhiteResult", success: true, users: whiteList);
                    break;

                default:
                    _logger.LogWarning("Unknown whitelist task type: {Type}", task.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing task {TaskId} failed", task.TaskId);
            await RetryTaskAsync(task); // 推送到死信队列
        }
    }

    private async Task RetryTaskAsync(WhiteListTask task)
    {
        var db = _redis.GetDatabase();
        var retryTask = JsonSerializer.Serialize(task);
        await db.ListRightPushAsync(FailedQueueKey, retryTask);
        _logger.LogInformation("Task {TaskId} failed. Pushed to dead-letter queue", task.TaskId);
    }

    private async Task PublishWhiteListEventAsync(WhiteListTask task, string action, bool success, List<string> users = null)
    {
        var eventMessage = new
        {
            EventType = action,
            task.DeviceId,
            task.TenantId,
            Success = success,
            Users = users,
            task.CardNo,
            task.PersonName,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        var db = _redis.GetDatabase();
        await db.PublishAsync($"device:events:{task.TenantId}", JsonSerializer.Serialize(eventMessage));
        _logger.LogInformation("Published event {EventType} for Device {DeviceId} to Redis", action, task.DeviceId);
    }
}
