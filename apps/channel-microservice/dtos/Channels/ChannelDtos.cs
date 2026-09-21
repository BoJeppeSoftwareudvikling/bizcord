namespace ChannelService.Dtos.Channels;

public sealed record ChannelDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateChannelDto(
    string? Name,
    string? Description);

public sealed record UpdateChannelDto(
    string? Name,
    string? Description);

public sealed record ChannelMemberDto(
    Guid ChannelId,
    Guid UserId,
    ChannelRoleDto Role,
    ChannelPermissionDto Permissions,
    DateTimeOffset JoinedAt);

public enum ChannelRoleDto
{
    Member = 0,
    Moderator = 1,
    Admin = 2
}

public sealed record ChannelPermissionDto(
    bool CanReadMessages,
    bool CanPostMessages,
    bool CanManageChannel,
    bool CanManageMembers);

public sealed record ChannelCreatedEventDto(
    Guid ChannelId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record ChannelUpdatedEventDto(
    Guid ChannelId,
    string Name,
    string? Description,
    DateTimeOffset UpdatedAt);

public sealed record ChannelDeletedEventDto(
    Guid ChannelId,
    DateTimeOffset DeletedAt);
