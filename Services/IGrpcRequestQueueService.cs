namespace GrpcService.Services
{
    public interface IGrpcRequestQueueService
    {
        Task<TResponse> EnqueueRequestAsync<TRequest, TResponse>(
            string deviceId,
            string requestType,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TResponse>> handler,
            CancellationToken cancellationToken = default);

        Dictionary<string, object> GetQueueStatistics();
    }
}
