namespace Messages;

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
