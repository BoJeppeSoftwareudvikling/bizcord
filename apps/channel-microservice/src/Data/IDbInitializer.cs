namespace ChannelService.Data;

public interface IDbInitializer
{
    void Initialize(ChannelContext context);
}
