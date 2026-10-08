using ChannelService.Application.Channels;
using Messages;

namespace ChannelService.Infrastructure.Messaging;

public sealed class MessagePostedHandler(
    ChannelManagementService channelService)
{
    private readonly ChannelManagementService _channelService =
        channelService
        ?? throw new ArgumentNullException(nameof(channelService));

    public Task Handle(
        MessagePostedEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.PostedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "MessagePostedEvent.PostedAt must be UTC.",
                nameof(message));
        }

        return _channelService.RecordMessageActivityAsync(
            message.ChannelId,
            message.MessageId,
            new DateTimeOffset(message.PostedAt),
            cancellationToken);
    }
}
