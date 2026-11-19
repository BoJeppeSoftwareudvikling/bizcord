using ChannelService.Application.Channels;
using ChannelService.Authentication;
using ChannelService.Dtos.Channels;
using HotChocolate.Authorization;

namespace ChannelService.GraphQL;

public sealed record CreateChannelInput(string Name, string? Description);

[Authorize(Policy = ServiceAuthorizationPolicies.ChannelWrite)]
public sealed class ChannelMutation
{
    public Task<ChannelDto> CreateChannelAsync(
        CreateChannelInput input,
        [Service] ChannelManagementService channelService,
        CancellationToken cancellationToken) =>
        channelService.CreateAsync(
            new CreateChannelCommand(input.Name, input.Description),
            cancellationToken);
}
