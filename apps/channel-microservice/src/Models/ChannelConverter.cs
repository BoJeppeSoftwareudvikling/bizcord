using SharedModels;

namespace ChannelService.Models;

public class ChannelConverter : IConverter<Channel, ChannelDto>
{
    public Channel Convert(ChannelDto model)
    {
        return new Channel
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            CreatedAt = model.CreatedAt
        };
    }

    public ChannelDto Convert(Channel model)
    {
        return new ChannelDto
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            CreatedAt = model.CreatedAt
        };
    }
}
