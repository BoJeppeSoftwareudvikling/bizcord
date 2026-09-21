namespace ChannelService.Application.Channels;

public sealed record CreateChannelCommand(
    string? Name,
    string? Description);

public sealed record UpdateChannelCommand(
    string? Name,
    string? Description);
