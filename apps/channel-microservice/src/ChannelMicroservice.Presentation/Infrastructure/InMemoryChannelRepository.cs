using System.Collections.Concurrent;
using ChannelService.Application.Abstractions;
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

        if (!_channels.TryAdd(channel.Id, channel))
        {
            throw new InvalidOperationException("Channel already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Channel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _channels.TryGetValue(id, out var channel);

        return Task.FromResult(channel);
    }

    public Task<IReadOnlyCollection<Channel>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<Channel> channels = _channels.Values
            .OrderBy(channel => channel.CreatedAt)
            .ToList();

        return Task.FromResult(channels);
    }

    public Task SaveAsync(
        Channel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        _channels[channel.Id] = channel;

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
}
