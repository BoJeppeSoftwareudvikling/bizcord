using System.Collections.Concurrent;
using ChannelService.Application.Abstractions;
using ChannelService.Application.Channels;
using ChannelService.Domain;

namespace ChannelService.Infrastructure;

public sealed class InMemoryChannelRepository : IChannelRepository
{
    private readonly ConcurrentDictionary<Guid, Channel> _channels = new();

    public Task AddAsync(
        Channel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = Copy(channel);
        stored.Version = 1;
        if (!_channels.TryAdd(channel.Id, stored))
        {
            throw new InvalidOperationException("Channel already exists.");
        }

        channel.Version = stored.Version;
        return Task.CompletedTask;
    }

    public Task<Channel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _channels.TryGetValue(id, out var channel);

        return Task.FromResult(channel is null ? null : Copy(channel));
    }

    public Task<IReadOnlyCollection<Channel>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Channel> channels = _channels.Values
            .OrderBy(channel => channel.CreatedAt)
            .Select(Copy)
            .ToList();

        return Task.FromResult(channels);
    }

    public Task SaveAsync(
        Channel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_channels.TryGetValue(channel.Id, out var current) ||
            current.Version != channel.Version)
        {
            throw new ChannelConcurrencyException(channel.Id);
        }

        var stored = Copy(channel);
        stored.Version = checked(current.Version + 1);
        if (!_channels.TryUpdate(channel.Id, stored, current))
        {
            throw new ChannelConcurrencyException(channel.Id);
        }

        channel.Version = stored.Version;

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = _channels.TryRemove(id, out _);

        return Task.FromResult(deleted);
    }

    private static Channel Copy(Channel channel) => new()
    {
        Id = channel.Id,
        Version = channel.Version,
        Name = channel.Name,
        Description = channel.Description,
        CreatedAt = channel.CreatedAt,
        UpdatedAt = channel.UpdatedAt,
        LastMessageId = channel.LastMessageId,
        LastActivityAt = channel.LastActivityAt,
        Members = channel.Members.Select(member => new ChannelMember
        {
            ChannelId = member.ChannelId,
            UserId = member.UserId,
            Role = member.Role,
            Permissions = member.Permissions,
            JoinedAt = member.JoinedAt
        }).ToList()
    };
}
