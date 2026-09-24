namespace ChannelService.Domain;

public sealed record ChannelPermissions(
    bool CanReadMessages,
    bool CanPostMessages,
    bool CanManageChannel,
    bool CanManageMembers);
