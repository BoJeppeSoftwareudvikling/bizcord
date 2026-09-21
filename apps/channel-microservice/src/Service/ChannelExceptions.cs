namespace ChannelService.Service;

public class ChannelNotFoundException : Exception
{
    public ChannelNotFoundException(string message) : base(message)
    {
    }
}
