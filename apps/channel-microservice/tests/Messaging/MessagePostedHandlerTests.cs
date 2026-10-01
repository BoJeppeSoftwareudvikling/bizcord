using ChannelService.Application.Abstractions;
using ChannelService.Application.Channels;
using ChannelService.Domain;
using ChannelService.Infrastructure.Messaging;
using MessageClient;
using Messages;
using Moq;

namespace ChannelService.Tests.Messaging;

public sealed class MessagePostedHandlerTests
{
    [Fact]
    public async Task Handle_CanConsumeMinimumValidContract()
    {
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var repository = new Mock<IChannelRepository>();
        var messageClient = new Mock<IMessageClient>();

        repository
            .Setup(candidate => candidate.GetAsync(
                channel.Id,
                CancellationToken.None))
            .ReturnsAsync(channel);

        repository
            .Setup(candidate => candidate.SaveAsync(
                channel,
                CancellationToken.None))
            .Returns(Task.CompletedTask);

        messageClient
            .Setup(candidate => candidate.PublishAsync(
                It.IsAny<ChannelActivityRecordedEventDto>(),
                CancellationToken.None))
            .Returns(Task.CompletedTask);

        var service = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        var handler = new MessagePostedHandler(service);

        var message = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = channel.Id,
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        };

        var exception = await Record.ExceptionAsync(
            () => handler.Handle(
                message,
                CancellationToken.None));

        Assert.Null(exception);

        messageClient.Verify(
            candidate => candidate.PublishAsync(
                It.Is<ChannelActivityRecordedEventDto>(
                    published =>
                        published.MessageId == message.MessageId &&
                        published.ChannelId == message.ChannelId),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsNonUtcPostedAt()
    {
        var repository = new Mock<IChannelRepository>();
        var messageClient = new Mock<IMessageClient>();

        var service = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        var handler = new MessagePostedHandler(service);

        var message = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.SpecifyKind(
                DateTime.Now,
                DateTimeKind.Local)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(
                message,
                CancellationToken.None));
    }
}
