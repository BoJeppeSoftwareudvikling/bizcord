using ChannelService.Domain;

namespace ChannelService.Application.Abstractions;

public interface IChannelRepository
{
    Task AddAsync(Channel channel, CancellationToken cancellationToken = default);

    // Returnerer en selvstændig kopi med den version, en senere SaveAsync skal matche.
    Task<Channel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Channel>> ListAsync(CancellationToken cancellationToken = default);

    // Afviser ændrede eller slettede kanaler med ChannelConcurrencyException.
    Task SaveAsync(Channel channel, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
