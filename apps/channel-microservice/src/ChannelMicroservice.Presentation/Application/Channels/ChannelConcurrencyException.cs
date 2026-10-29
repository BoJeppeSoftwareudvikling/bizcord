namespace ChannelService.Application.Channels;

public sealed class ChannelConcurrencyException(Guid channelId)
    : Exception($"Channel '{channelId}' changed or was deleted during the operation.");
