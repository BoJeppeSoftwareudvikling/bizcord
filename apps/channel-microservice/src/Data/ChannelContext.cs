using ChannelService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChannelService.Data;

public class ChannelContext : DbContext
{
    public ChannelContext(DbContextOptions<ChannelContext> options) : base(options)
    {
    }

    public DbSet<Channel> Channels { get; set; }
}
