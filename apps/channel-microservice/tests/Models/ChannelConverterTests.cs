using ChannelService.Models;
using SharedModels;

namespace ChannelService.Tests.Models;

public sealed class ChannelConverterTests
{
    private readonly ChannelConverter _converter = new();

    [Fact]
    public void Convert_ChannelDto_MapsAllSharedFields()
    {
        var createdAt = DateTime.UtcNow;
        var dto = new ChannelDto
        {
            Id = 7,
            Name = "Architecture",
            Description = "Architecture discussions",
            CreatedAt = createdAt
        };

        Channel channel = _converter.Convert(dto);

        Assert.Equal(dto.Id, channel.Id);
        Assert.Equal(dto.Name, channel.Name);
        Assert.Equal(dto.Description, channel.Description);
        Assert.Equal(createdAt, channel.CreatedAt);
    }

    [Fact]
    public void Convert_Channel_MapsAllSharedFields()
    {
        var createdAt = DateTime.UtcNow;
        var channel = new Channel
        {
            Id = 8,
            Name = "Support",
            Description = "Support discussions",
            CreatedAt = createdAt
        };

        ChannelDto dto = _converter.Convert(channel);

        Assert.Equal(channel.Id, dto.Id);
        Assert.Equal(channel.Name, dto.Name);
        Assert.Equal(channel.Description, dto.Description);
        Assert.Equal(createdAt, dto.CreatedAt);
    }
}
