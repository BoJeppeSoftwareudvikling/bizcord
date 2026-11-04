using System.Reflection;
using ChannelService.Application.Abstractions;
using ChannelService.Application.Channels;
using ChannelService.Authentication;
using ChannelService.Controllers;
using ChannelService.Domain;
using ChannelService.Dtos.Channels;
using MessageClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ChannelService.Tests.Presentation.Controllers;

public sealed class InternalChannelsControllerTests
{
    [Fact]
    public async Task Exists_ReturnsExistingChannel()
    {
        var channelId = Guid.NewGuid();
        var repository = new Mock<IChannelRepository>();
        repository
            .Setup(instance => instance.GetAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Channel
            {
                Id = channelId,
                Name = "security",
                CreatedAt = DateTimeOffset.UtcNow
            });
        var controller = CreateController(repository);

        var result = await controller.Exists(channelId, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ChannelExistenceDto>(response.Value);
        Assert.Equal(channelId, body.ChannelId);
        Assert.True(body.Exists);
    }

    [Fact]
    public async Task Exists_ReturnsNotFoundForUnknownChannel()
    {
        var channelId = Guid.NewGuid();
        var repository = new Mock<IChannelRepository>();
        repository
            .Setup(instance => instance.GetAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);
        var controller = CreateController(repository);

        var result = await controller.Exists(channelId, CancellationToken.None);

        var response = Assert.IsType<NotFoundObjectResult>(result.Result);
        var body = Assert.IsType<ChannelExistenceDto>(response.Value);
        Assert.Equal(channelId, body.ChannelId);
        Assert.False(body.Exists);
    }

    [Fact]
    public void Controller_RequiresInternalServicePolicy()
    {
        var attribute = typeof(InternalChannelsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(ServiceAuthorizationPolicies.InternalService, attribute.Policy);
    }

    private static InternalChannelsController CreateController(
        Mock<IChannelRepository> repository)
    {
        var messageClient = new Mock<IMessageClient>();
        var service = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        return new InternalChannelsController(service);
    }
}
