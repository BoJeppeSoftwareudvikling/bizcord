using ChannelService.Service;
using Microsoft.AspNetCore.Mvc;
using SharedModels;

namespace ChannelService.Controllers;

[ApiController]
[Route("[controller]")]
public class ChannelController : ControllerBase
{
    private readonly IChannelService _channelService;

    public ChannelController(IChannelService channelService)
    {
        _channelService = channelService;
    }

    [HttpGet]
    public IEnumerable<ChannelDto> Get()
    {
        return _channelService.GetChannels();
    }

    [HttpGet("{id:int}", Name = "GetById")]
    public IActionResult GetById(int id)
    {
        ChannelDto? channel = _channelService.GetChannelById(id);
        return channel == null ? NotFound() : Ok(channel);
    }

    [HttpPost]
    public ActionResult Post([FromBody] ChannelDto channel)
    {
        ChannelDto newChannel = _channelService.CreateChannel(channel);
        return CreatedAtRoute(nameof(GetById), new { id = newChannel.Id }, newChannel);
    }

    [HttpPut("{id:int}")]
    public IActionResult Put(int id, [FromBody] ChannelDto channel)
    {
        try
        {
            ChannelDto updatedChannel = _channelService.UpdateChannel(id, channel);
            return Ok(updatedChannel);
        }
        catch (ChannelNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try
        {
            _channelService.DeleteChannel(id);
            return NoContent();
        }
        catch (ChannelNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }
}
