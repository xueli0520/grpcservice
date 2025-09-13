namespace GrpcService.Models
{
    public class WhiteListTask
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
        public string? Type { get; set; }
        public string? TenantId { get; set; }
        public string? DeviceId { get; set; }
        public string? CardNo { get; set; }
        public string? PersonName { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }
}
