using ChannelService.Application.Channels;
using ChannelService.Authentication;
using ChannelService.Dtos.Channels;
using HotChocolate.Authorization;

namespace ChannelService.GraphQL;

[Authorize(Policy = ServiceAuthorizationPolicies.ChannelRead)]
public sealed class ChannelQuery
{
    public Task<IReadOnlyCollection<ChannelDto>> GetChannelsAsync(
        [Service] ChannelManagementService channelService,
        CancellationToken cancellationToken) =>
        channelService.ListAsync(cancellationToken);

    public Task<ChannelDto?> GetChannelAsync(
        Guid id,
        [Service] ChannelManagementService channelService,
        CancellationToken cancellationToken) =>
        channelService.GetAsync(id, cancellationToken);
}
