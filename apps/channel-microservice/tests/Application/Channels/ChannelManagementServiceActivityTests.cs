using ChannelService.Application.Abstractions;
using ChannelService.Application.Channels;
using ChannelService.Domain;
using MessageClient;
using Messages;
using Moq;

namespace ChannelService.Tests.Application.Channels;

public sealed class ChannelManagementServiceActivityTests
{
    [Fact]
    public async Task RecordMessageActivityAsync_UpdatesChannel_AndPublishesEvent()
    {
        var channel = CreateChannel();
        var messageId = Guid.NewGuid();
        var activityAt = DateTimeOffset.UtcNow;
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

        await service.RecordMessageActivityAsync(
            channel.Id,
            messageId,
            activityAt,
            CancellationToken.None);

        Assert.Equal(messageId, channel.LastMessageId);
        Assert.Equal(activityAt, channel.LastActivityAt);

        repository.Verify(
            candidate => candidate.SaveAsync(
                channel,
                CancellationToken.None),
            Times.Once);

        messageClient.Verify(
            candidate => candidate.PublishAsync(
                It.Is<ChannelActivityRecordedEventDto>(
                    published =>
                        published.MessageId == messageId &&
                        published.ChannelId == channel.Id &&
                        published.ActivityAt == activityAt),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task RecordMessageActivityAsync_Throws_WhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        var repository = new Mock<IChannelRepository>();
        var messageClient = new Mock<IMessageClient>();

        repository
            .Setup(candidate => candidate.GetAsync(
                channelId,
                CancellationToken.None))
            .ReturnsAsync((Channel?)null);

        var service = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordMessageActivityAsync(
                channelId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.Contains(channelId.ToString(), exception.Message);

        messageClient.Verify(
            candidate => candidate.PublishAsync(
                It.IsAny<ChannelActivityRecordedEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordMessageActivityAsync_RejectsEmptyMessageId()
    {
        var repository = new Mock<IChannelRepository>();
        var messageClient = new Mock<IMessageClient>();
        var service = new ChannelManagementService(
            repository.Object,
            messageClient.Object);

        await Assert.ThrowsAsync<ChannelValidationException>(
            () => service.RecordMessageActivityAsync(
                Guid.NewGuid(),
                Guid.Empty,
                DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    private static Channel CreateChannel()
    {
        return new Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
