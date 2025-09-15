using System.Collections.Concurrent;

namespace GrpcService.Infrastructure
{
    /// <summary>
    /// 多租户并发控制：不同租户并行，租户内部可配置最大并发数。
    /// </summary>
    public class TenantConcurrencyManager
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _tenantLocks = new();
        private readonly int _maxConcurrencyPerTenant;
        private readonly Dictionary<string, string> _deviceTenantMap;

        public TenantConcurrencyManager(IConfiguration configuration)
        {
            _maxConcurrencyPerTenant = configuration.GetValue<int>("Hikvision:MaxDeviceConcurrentPerTenant", Environment.ProcessorCount);
            if (_maxConcurrencyPerTenant <= 0)
            {
                _maxConcurrencyPerTenant = Environment.ProcessorCount;
            }

            _deviceTenantMap = configuration.GetSection("DeviceTenantMap").Get<Dictionary<string, string>>() ?? [];
        }

        private SemaphoreSlim GetTenantSemaphore(string tenantId)
        {
            return _tenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(_maxConcurrencyPerTenant, _maxConcurrencyPerTenant));
        }

        public async Task<IDisposable> AcquireAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            var tenantId = ResolveTenantId(deviceId);
            var semaphore = GetTenantSemaphore(tenantId);
            await semaphore.WaitAsync(cancellationToken);
            return new Releaser(semaphore);
        }

        private string ResolveTenantId(string deviceId)
        {
            if (_deviceTenantMap.TryGetValue(deviceId, out var tenantId))
            {
                return tenantId;
            }
            return $"tenant-{deviceId}"; // 默认每个设备单独租户
        }

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
        {
            private readonly SemaphoreSlim _semaphore = semaphore;
            public void Dispose() => _semaphore.Release();
        }
    }
}
