using ChannelService.Domain;

namespace ChannelService.Application.Abstractions;

public interface IChannelRepository
{
    Task AddAsync(Channel channel, CancellationToken cancellationToken = default);

    Task<Channel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Channel>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(Channel channel, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
