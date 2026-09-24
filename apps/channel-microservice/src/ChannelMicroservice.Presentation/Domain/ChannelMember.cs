namespace ChannelService.Domain;

public sealed class ChannelMember
{
    public Guid ChannelId { get; set; }

    public Guid UserId { get; set; }

    public ChannelRole Role { get; set; } = ChannelRole.Member;

    public ChannelPermissions Permissions { get; set; } = new(
        CanReadMessages: true,
        CanPostMessages: true,
        CanManageChannel: false,
        CanManageMembers: false);

    public DateTimeOffset JoinedAt { get; set; }
}
