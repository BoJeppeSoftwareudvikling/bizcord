using ChannelService.Controllers;
using ChannelService.Service;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SharedModels;

namespace ChannelService.Tests.Controllers;

public sealed class ChannelControllerTests
{
    private readonly IChannelService _service = Substitute.For<IChannelService>();

    [Fact]
    public void GetById_ReturnsNotFoundWhenChannelDoesNotExist()
    {
        _service.GetChannelById(42).Returns((ChannelDto?)null);
        var controller = new ChannelController(_service);

        IActionResult result = controller.GetById(42);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Post_ReturnsCreatedAtRouteWithCreatedChannel()
    {
        var request = new ChannelDto { Name = "Announcements" };
        var created = new ChannelDto { Id = 3, Name = "Announcements" };
        _service.CreateChannel(request).Returns(created);
        var controller = new ChannelController(_service);

        ActionResult result = controller.Post(request);

        var response = Assert.IsType<CreatedAtRouteResult>(result);
        Assert.Equal("GetById", response.RouteName);
        Assert.Equal(created, response.Value);
    }

    [Fact]
    public void Put_ReturnsNotFoundWhenChannelDoesNotExist()
    {
        var request = new ChannelDto { Name = "Missing" };
        _service.UpdateChannel(42, request)
            .Returns(_ => throw new ChannelNotFoundException("Channel with id 42 was not found"));
        var controller = new ChannelController(_service);

        IActionResult result = controller.Put(42, request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Delete_ReturnsNoContentForExistingChannel()
    {
        var controller = new ChannelController(_service);

        IActionResult result = controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        _service.Received(1).DeleteChannel(1);
    }
}
