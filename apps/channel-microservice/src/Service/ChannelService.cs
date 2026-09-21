using ChannelService.Data;
using ChannelService.Models;
using SharedModels;

namespace ChannelService.Service;

public class ChannelService : IChannelService
{
    private readonly IChannelRepository _channelRepository;
    private readonly IConverter<Channel, ChannelDto> _channelConverter;

    public ChannelService(
        IChannelRepository channelRepository,
        IConverter<Channel, ChannelDto> channelConverter)
    {
        _channelRepository = channelRepository;
        _channelConverter = channelConverter;
    }

    public IEnumerable<ChannelDto> GetChannels()
    {
        return _channelRepository.GetAll().Select(channel => _channelConverter.Convert(channel));
    }

    public ChannelDto? GetChannelById(int id)
    {
        Channel? channel = _channelRepository.GetById(id);
        return channel == null ? null : _channelConverter.Convert(channel);
    }

    public ChannelDto CreateChannel(ChannelDto channel)
    {
        var newChannel = _channelRepository.Create(new Channel
        {
            Name = channel.Name,
            Description = channel.Description,
            CreatedAt = DateTime.UtcNow
        });

        return _channelConverter.Convert(newChannel);
    }

    public ChannelDto UpdateChannel(int id, ChannelDto channel)
    {
        Channel? existingChannel = _channelRepository.GetById(id);
        if (existingChannel == null)
        {
            throw new ChannelNotFoundException($"Channel with id {id} was not found");
        }

        existingChannel.Name = channel.Name;
        existingChannel.Description = channel.Description;
        _channelRepository.Update(existingChannel);
        return _channelConverter.Convert(existingChannel);
    }

    public void DeleteChannel(int id)
    {
        Channel? channel = _channelRepository.GetById(id);
        if (channel == null)
        {
            throw new ChannelNotFoundException($"Channel with id {id} was not found");
        }

        _channelRepository.Delete(channel);
    }
}
