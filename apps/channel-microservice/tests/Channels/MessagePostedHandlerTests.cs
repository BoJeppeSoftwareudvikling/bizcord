using ChannelService.Application.Channels;
using ChannelService.Infrastructure;
using FluentAssertions;
using Messages;

namespace ChannelService.Tests.Channels;

public sealed class MessagePostedHandlerTests
{
    [Fact]
    public async Task Handle_PublishesResultEvent_WithCorrectMessageId()
    {
        var messageId = Guid.NewGuid();
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(client, new InMemoryChannelRepository());

        await handler.Handle(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var published = client.SinglePublished<ChannelActivityRecordedEvent>();
        published.Should().NotBeNull();
        published!.MessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(client, new InMemoryChannelRepository());

        Func<Task> act = () => handler.Handle(new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        }, CancellationToken.None);

        await act.Should().NotThrowAsync();
        client.Published.Should().HaveCount(1);
    }
}
