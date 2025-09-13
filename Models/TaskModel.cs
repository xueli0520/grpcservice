namespace GrpcService.Models
{
    public class TaskRequestItem
    {
        public string DeviceId { get; set; } = string.Empty;
        public Func<object, CancellationToken, Task<object>> Handler { get; set; } = default!;
        public object Request { get; set; } = default!;
        public string RequestId { get; set; } = string.Empty;
        public TaskCompletionSource<object> TaskCompletionSource { get; set; } = default!;
        public CancellationToken CancellationToken { get; set; }
    }
}
