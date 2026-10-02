using FluentAssertions;
using Messages;

namespace ChannelService.Tests.Channels;

public sealed class MessagePostedIntegrationTests : IClassFixture<ChannelServiceFactory>
{
    private readonly ChannelServiceFactory _factory;

    public MessagePostedIntegrationTests(ChannelServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
    {
        var messageId = Guid.NewGuid();
        using var capture = new MessageCapture<ChannelActivityRecordedEvent>(_factory.MessageClient);

        await _factory.MessageClient.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        });

        var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));

        result.MessageId.Should().Be(messageId);
    }
}
