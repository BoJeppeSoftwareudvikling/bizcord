using ChannelService.Application.Abstractions;
using MessageClient;
using Messages;

namespace ChannelService.Application.Channels;

public interface IMessageHandler<TMessage>
{
    Task Handle(TMessage message, CancellationToken cancellationToken);
}

public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly IMessageClient _messageClient;
    private readonly IChannelRepository _channelRepository;

    public MessagePostedHandler(
        IMessageClient messageClient,
        IChannelRepository channelRepository)
    {
        _messageClient = messageClient;
        _channelRepository = channelRepository;
    }

    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        var channel = await _channelRepository.GetAsync(message.ChannelId, cancellationToken);

        var activityAt = new DateTimeOffset(
            DateTime.SpecifyKind(message.PostedAt, DateTimeKind.Utc));

        if (channel is not null)
        {
            channel.LastMessageId = message.MessageId;
            channel.LastActivityAt = activityAt;
            await _channelRepository.SaveAsync(channel, cancellationToken);
        }

        await _messageClient.PublishAsync(
            new ChannelActivityRecordedEvent(
                message.MessageId,
                message.ChannelId,
                activityAt),
            cancellationToken);
    }
}
