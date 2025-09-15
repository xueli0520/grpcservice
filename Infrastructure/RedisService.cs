using GrpcService.Api;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace GrpcService.Infrastructure
{
    /// <summary>
    /// Redis 服务封装，支持基本 KV、List、PubSub。
    /// </summary>
    public class RedisService(IConnectionMultiplexer redisConnection, ILogger<HkDeviceService> logger) : IDisposable
    {
        private readonly IConnectionMultiplexer _redisConnection = redisConnection ?? throw new ArgumentNullException(nameof(redisConnection));
        private IDatabase Db => _redisConnection.GetDatabase();
        private readonly ILogger<HkDeviceService> _logger = logger;
        // 执行 Set 操作
        public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null, bool keepTtl = false)
        {
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    return await Db.StringSetAsync(key, value, expiry, keepTtl);
                }
                catch (RedisConnectionException)
                {
                    retryCount--;
                    if (retryCount == 0) throw;
                    await Task.Delay(1000); // 可以使用指数退避机制
                }
            }
            return false;
        }

        // 获取字符串值
        public async Task<string?> GetStringAsync(string key)
        {
            return await Db.StringGetAsync(key);
        }

        // 向列表左侧推送元素
        public async Task<long> ListLeftPushAsync(string key, string value)
        {
            return await Db.ListLeftPushAsync(key, value);
        }

        // 从列表右侧弹出元素
        public async Task<RedisValue> ListRightPopAsync(string key)
        {
            return await Db.ListRightPopAsync(key);
        }

        // 发布消息到频道
        public async Task PublishAsync(string channel, string message)
        {
            var sub = _redisConnection.GetSubscriber();
            await sub.PublishAsync(channel, message);
        }

        // 订阅消息并处理
        public async Task Subscribe(string channel, Action<RedisChannel, RedisValue> handler)
        {
            var sub = _redisConnection.GetSubscriber();
            await Task.Run(async () => await sub.SubscribeAsync(channel, handler));  // 在后台运行
        }

        // 确认流消息
        public async Task AcknowledgeStreamMessage(string streamKey, string consumerGroup, string messageId)
        {
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    await Db.StreamAcknowledgeAsync(streamKey, consumerGroup, messageId);
                    break;
                }
                catch (RedisException ex)
                {
                    retryCount--;
                    if (retryCount == 0) throw;
                    await Task.Delay(500);
                }
            }
        }

        // 批量订阅多个频道
        public async Task SubscribeToMultipleChannels(string[] channels, Action<RedisChannel, RedisValue> handler)
        {
            var sub = _redisConnection.GetSubscriber();
            foreach (var channel in channels)
            {
                await Task.Run(() => sub.SubscribeAsync(channel, handler));  // 在后台运行
            }
        }

        public async Task<List<StreamEntry>> StreamReadGroupAsync(string streamKey, string consumerGroup, string consumerName, string start, int count = 10, bool noAck = false)
        {
            var db = redisConnection.GetDatabase();
            var result = new List<StreamEntry>();

            try
            {
                var stream = await db.StreamReadGroupAsync(streamKey, consumerGroup, consumerName, start, count, noAck);
                foreach (var streamEntry in stream)
                {
                    //foreach (var entry in streamEntry.Values)
                    //{
                    //    result.Add(entry);
                    //}
                    result.Add(streamEntry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading stream");
            }

            return result;
        }

        /// <summary>
        /// 创建一个新的消费者组
        /// </summary>
        public async Task StreamCreateConsumerGroupAsync(string streamKey, string consumerGroup, string id = "0", bool createIfNotExists = true)
        {
            var db = redisConnection.GetDatabase();
            try
            {
                // 创建消费者组
                await db.StreamCreateConsumerGroupAsync(streamKey, consumerGroup, id, createIfNotExists);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                Console.WriteLine("Consumer group already exists.");
            }
        }

        // 删除消费者
        public async Task StreamDeleteConsumerAsync(string streamKey, string consumerGroup, string consumerName)
        {
            var db = redisConnection.GetDatabase();
            try
            {
                await db.ExecuteAsync("XGROUP", "DELCONSUMER", streamKey, consumerGroup, consumerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting consumer");
            }
        }

        public void Dispose()
        {
            _redisConnection?.Dispose();
            GC.SuppressFinalize(this); 
        }
    }
}
