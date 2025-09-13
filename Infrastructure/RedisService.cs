using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace GrpcService.Infrastructure
{
    /// <summary>
    /// Redis 服务封装，支持基本 KV、List、PubSub。
    /// </summary>
    public class RedisService : IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisService(string connectionString)
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            return await _db.StringSetAsync(key, value, expiry);
        }

        public async Task<string?> GetStringAsync(string key)
        {
            return await _db.StringGetAsync(key);
        }

        public async Task<long> ListLeftPushAsync(string key, string value)
        {
            return await _db.ListLeftPushAsync(key, value);
        }

        public async Task<RedisValue> ListRightPopAsync(string key)
        {
            return await _db.ListRightPopAsync(key);
        }

        public async Task PublishAsync(string channel, string message)
        {
            var sub = _redis.GetSubscriber();
            await sub.PublishAsync(channel, message);
        }

        public void Subscribe(string channel, Action<RedisChannel, RedisValue> handler)
        {
            var sub = _redis.GetSubscriber();
            sub.Subscribe(channel, handler);
        }


        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}
