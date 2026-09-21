using SharedModels;

namespace ChannelService.Service;

public interface IChannelService
{
    IEnumerable<ChannelDto> GetChannels();
    ChannelDto? GetChannelById(int id);
    ChannelDto CreateChannel(ChannelDto channel);
    ChannelDto UpdateChannel(int id, ChannelDto channel);
    void DeleteChannel(int id);
}
