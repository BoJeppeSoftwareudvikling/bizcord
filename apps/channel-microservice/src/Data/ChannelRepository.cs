using ChannelService.Models;

namespace ChannelService.Data;

public class ChannelRepository : IChannelRepository
{
    private readonly ChannelContext _context;

    public ChannelRepository(ChannelContext context)
    {
        _context = context;
    }

    public Channel Create(Channel channel)
    {
        Channel newChannel = _context.Channels.Add(channel).Entity;
        _context.SaveChanges();
        return newChannel;
    }

    public void Update(Channel channel)
    {
        _context.Channels.Update(channel);
        _context.SaveChanges();
    }

    public Channel? GetById(int id)
    {
        return _context.Channels.Find(id);
    }

    public IEnumerable<Channel> GetAll()
    {
        return _context.Channels.ToList();
    }

    public void Delete(Channel channel)
    {
        _context.Channels.Remove(channel);
        _context.SaveChanges();
    }
}
