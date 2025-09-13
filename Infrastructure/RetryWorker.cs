using GrpcService.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace GrpcService.Infrastructure;
public class RetryWorker : BackgroundService
{
    private readonly ILogger<RetryWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly TenantAwareDeviceManager _deviceManager;

    private const string FailedQueueKey = "whitelist:failed";  // 死信队列
    private const int MaxRetryCount = 3;  // 最大重试次数
    private const int RetryIntervalSeconds = 10;  // 每次重试间隔，单位：秒

    public RetryWorker(
        ILogger<RetryWorker> logger,
        IConnectionMultiplexer redis,
        TenantAwareDeviceManager deviceManager)
    {
        _logger = logger;
        _redis = redis;
        _deviceManager = deviceManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        _logger.LogInformation("RetryWorker started, listening Redis dead-letter queue {FailedQueueKey}", FailedQueueKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 从死信队列中拉取任务
                var redisValue = await db.ListLeftPopAsync(FailedQueueKey);
                if (redisValue.IsNullOrEmpty)
                {
                    await Task.Delay(500, stoppingToken); // 无任务时等待
                    continue;
                }

                var task = JsonSerializer.Deserialize<WhiteListTask>(redisValue);
                if (task == null)
                {
                    _logger.LogWarning("Invalid task in dead-letter queue: {Value}", redisValue);
                    continue;
                }

                await ProcessRetryTaskAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing retry task");
            }
        }
    }

    private async Task ProcessRetryTaskAsync(WhiteListTask task)
    {
        // 查询任务的重试次数
        var retryCountKey = $"whitelist:retryCount:{task.TaskId}";
        var db = _redis.GetDatabase();
        int retryCount = (int)(await db.StringGetAsync(retryCountKey));

        if (retryCount >= MaxRetryCount)
        {
            _logger.LogWarning("Task {TaskId} reached max retry count. Giving up.", task.TaskId);
            return;  // 超过最大重试次数，停止重试
        }

        try
        {
            _logger.LogInformation("Retrying task {TaskId}, RetryCount={RetryCount}", task.TaskId, retryCount);

            bool success = false;
            switch (task.Type)
            {
                case "UpdateWhite":
                    success = await _deviceManager.UpdateWhiteAsync(task.TenantId, task.DeviceId, task.CardNo, task.PersonName);
                    break;

                case "DeleteWhite":
                    success = await _deviceManager.DeleteWhiteAsync(task.TenantId, task.DeviceId, task.CardNo);
                    break;

                case "PageWhite":
                    success = true;  // Query task, no need to retry if failed
                    break;

                default:
                    _logger.LogWarning("Unknown task type: {Type}", task.Type);
                    return;
            }

            // 更新重试计数
            await db.StringIncrementAsync(retryCountKey);

            if (success)
            {
                _logger.LogInformation("Task {TaskId} retried successfully", task.TaskId);
                await db.ListRightPushAsync("whitelist:queue", JsonSerializer.Serialize(task));  // 放回正常队列
            }
            else
            {
                _logger.LogWarning("Task {TaskId} retry failed. Waiting for next attempt", task.TaskId);
                // 任务失败，等待下一次重试
                await Task.Delay(TimeSpan.FromSeconds(RetryIntervalSeconds));
                await db.ListRightPushAsync(FailedQueueKey, JsonSerializer.Serialize(task)); // 放回死信队列继续重试
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrying task {TaskId} failed", task.TaskId);
            await Task.Delay(TimeSpan.FromSeconds(RetryIntervalSeconds));  // 失败后等待再重试
        }
    }
}
