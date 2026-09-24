using ChannelService.Application.Channels;
using ChannelService.Dtos.Channels;
using Microsoft.AspNetCore.Mvc;

namespace ChannelService.Controllers;

[ApiController]
[Route("channels")]
public sealed class ChannelsController(ChannelManagementService channelService) : ControllerBase
{
    private readonly ChannelManagementService _channelService =
        channelService ?? throw new ArgumentNullException(nameof(channelService));

    [HttpPost]
    public async Task<ActionResult<ChannelDto>> CreateChannel(
        CreateChannelDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _channelService.CreateAsync(
                new CreateChannelCommand(request.Name, request.Description),
                cancellationToken);

            return CreatedAtAction(
                nameof(GetChannel),
                new { id = channel.Id },
                channel);
        }
        catch (ChannelValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ChannelDto>>> GetChannels(
        CancellationToken cancellationToken)
    {
        var channels = await _channelService.ListAsync(cancellationToken);

        return Ok(channels);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChannelDto>> GetChannel(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _channelService.GetAsync(id, cancellationToken);

            if (channel is null)
            {
                return NotFound();
            }

            return Ok(channel);
        }
        catch (ChannelValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ChannelDto>> UpdateChannel(
        Guid id,
        UpdateChannelDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _channelService.UpdateAsync(
                id,
                new UpdateChannelCommand(request.Name, request.Description),
                cancellationToken);

            if (channel is null)
            {
                return NotFound();
            }

            return Ok(channel);
        }
        catch (ChannelValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChannel(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _channelService.DeleteAsync(id, cancellationToken);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ChannelValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
