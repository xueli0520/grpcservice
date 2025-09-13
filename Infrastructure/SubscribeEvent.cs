using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GrpcService.Infrastructure;
public class SubscribeEvent
{
    private readonly Channel<DeviceEvent> _channel = Channel.CreateUnbounded<DeviceEvent>();
    public ChannelReader<DeviceEvent> Subscribe()
    {
        return _channel.Reader;
    }

    public void Publish(DeviceEvent evt)
    {
        _channel.Writer.TryWrite(evt);
    }

}
