using ChannelService.Application.Channels;
using ChannelService.Authentication;
using ChannelService.Dtos.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChannelService.Controllers;

[ApiController]
[Route("internal/channels")]
[Authorize(Policy = ServiceAuthorizationPolicies.InternalService)]
public sealed class InternalChannelsController(ChannelManagementService channelService)
    : ControllerBase
{
    private readonly ChannelManagementService _channelService =
        channelService ?? throw new ArgumentNullException(nameof(channelService));

    [HttpGet("{id:guid}/exists")]
    public async Task<ActionResult<ChannelExistenceDto>> Exists(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _channelService.GetAsync(id, cancellationToken);
            var response = new ChannelExistenceDto(id, channel is not null);

            return channel is null
                ? NotFound(response)
                : Ok(response);
        }
        catch (ChannelValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
