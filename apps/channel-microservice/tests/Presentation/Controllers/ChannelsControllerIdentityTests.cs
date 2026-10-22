using ChannelService.Application.Abstractions;
using ChannelService.Application.Channels;
using ChannelService.Controllers;
using ChannelService.Dtos.Channels;
using MessageClient;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ChannelService.Tests.Presentation.Controllers;

public sealed class ChannelsControllerIdentityTests
{
    [Fact]
    public void GetCurrentUser_ReturnsForwardedIdentity()
    {
        var controller = CreateController();

        var result = controller.GetCurrentUser(
            "student-123",
            "member");

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var currentUser = Assert.IsType<CurrentUserDto>(response.Value);

        Assert.Equal("student-123", currentUser.UserId);
        Assert.Equal("member", currentUser.Role);
    }

    [Theory]
    [InlineData(null, "member")]
    [InlineData("student-123", null)]
    [InlineData("", "member")]
    [InlineData("student-123", "")]
    public void GetCurrentUser_ReturnsBadRequest_WhenIdentityIsIncomplete(
        string? userId,
        string? role)
    {
        var controller = CreateController();

        var result = controller.GetCurrentUser(userId, role);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static ChannelsController CreateController()
    {
        var repository = new Mock<IChannelRepository>();
        var messageClient = new Mock<IMessageClient>();
        var channelService = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        return new ChannelsController(channelService);
    }
}
