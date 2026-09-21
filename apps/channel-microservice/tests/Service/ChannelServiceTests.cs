using ChannelService.Data;
using ChannelService.Models;
using ChannelService.Service;
using NSubstitute;
using SharedModels;

namespace ChannelService.Tests.Service;

public sealed class ChannelServiceTests
{
    private readonly IChannelRepository _repository = Substitute.For<IChannelRepository>();
    private readonly ChannelConverter _converter = new();

    [Fact]
    public void CreateChannel_AssignsServerValuesAndReturnsDto()
    {
        var beforeCreate = DateTime.UtcNow;
        _repository.Create(Arg.Any<Channel>()).Returns(callInfo =>
        {
            Channel channel = callInfo.Arg<Channel>();
            channel.Id = 3;
            return channel;
        });
        var service = new ChannelService.Service.ChannelService(_repository, _converter);
        var request = new ChannelDto
        {
            Id = 99,
            Name = "Announcements",
            Description = "Company announcements",
            CreatedAt = DateTime.MinValue
        };

        ChannelDto result = service.CreateChannel(request);

        Assert.Equal(3, result.Id);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.True(result.CreatedAt >= beforeCreate);
    }

    [Fact]
    public void UpdateChannel_UpdatesEditableFieldsAndPreservesCreatedAt()
    {
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var channel = new Channel
        {
            Id = 1,
            Name = "Old name",
            Description = "Old description",
            CreatedAt = createdAt
        };
        _repository.GetById(1).Returns(channel);
        var service = new ChannelService.Service.ChannelService(_repository, _converter);

        ChannelDto result = service.UpdateChannel(1, new ChannelDto
        {
            Name = "New name",
            Description = "New description",
            CreatedAt = DateTime.UtcNow
        });

        Assert.Equal("New name", result.Name);
        Assert.Equal("New description", result.Description);
        Assert.Equal(createdAt, result.CreatedAt);
        _repository.Received(1).Update(channel);
    }

    [Fact]
    public void UpdateChannel_ThrowsWhenChannelDoesNotExist()
    {
        _repository.GetById(42).Returns((Channel?)null);
        var service = new ChannelService.Service.ChannelService(_repository, _converter);

        Assert.Throws<ChannelNotFoundException>(() =>
            service.UpdateChannel(42, new ChannelDto { Name = "Missing" }));
    }

    [Fact]
    public void DeleteChannel_DeletesExistingChannel()
    {
        var channel = new Channel { Id = 2, Name = "Temporary" };
        _repository.GetById(2).Returns(channel);
        var service = new ChannelService.Service.ChannelService(_repository, _converter);

        service.DeleteChannel(2);

        _repository.Received(1).Delete(channel);
    }

    [Fact]
    public void DeleteChannel_ThrowsWhenChannelDoesNotExist()
    {
        _repository.GetById(42).Returns((Channel?)null);
        var service = new ChannelService.Service.ChannelService(_repository, _converter);

        Assert.Throws<ChannelNotFoundException>(() => service.DeleteChannel(42));
    }
}
