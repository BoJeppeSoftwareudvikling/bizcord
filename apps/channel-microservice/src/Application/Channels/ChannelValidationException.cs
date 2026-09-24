namespace ChannelService.Application.Channels;

public sealed class ChannelValidationException(string message) : Exception(message);
