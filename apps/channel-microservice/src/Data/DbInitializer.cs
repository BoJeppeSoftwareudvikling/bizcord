using ChannelService.Models;

namespace ChannelService.Data;

public class DbInitializer : IDbInitializer
{
    public void Initialize(ChannelContext context)
    {
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        if (context.Channels.Any())
        {
            return;
        }

        var channels = new List<Channel>
        {
            new()
            {
                Name = "General",
                Description = "General company discussion",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Name = "Development",
                Description = "Software development discussion",
                CreatedAt = DateTime.UtcNow
            }
        };

        context.Channels.AddRange(channels);
        context.SaveChanges();
    }
}
