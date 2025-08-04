using Grpc.Core;
using GrpcService;

namespace GrpcService.Services
{
    public class HikDeviceService(ILogger<HikDeviceService> logger) 
    {
        private readonly ILogger<HikDeviceService> _logger = logger;

    }
}
