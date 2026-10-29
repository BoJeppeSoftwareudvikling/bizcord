using ChannelService.Application.Channels;
using ChannelService.Domain;
using ChannelService.Infrastructure;

namespace ChannelService.Tests.Infrastructure;

public sealed class InMemoryChannelRepositoryTests
{
    [Fact]
    public async Task ReadsAndWrites_DoNotExposeStoredMutableObjects()
    {
        var repository = new InMemoryChannelRepository();
        var channel = NewChannel();
        channel.Members.Add(new ChannelMember { UserId = Guid.NewGuid() });
        await repository.AddAsync(channel);
        channel.Name = "changed outside repository";
        channel.Members[0].Role = ChannelRole.Admin;

        var read = (await repository.GetAsync(channel.Id))!;
        Assert.Equal("general", read.Name);
        Assert.Equal(ChannelRole.Member, read.Members[0].Role);
        read.Name = "saved";
        await repository.SaveAsync(read);
        read.Name = "changed after save";
        read.Members.Clear();
        var listed = Assert.Single(await repository.ListAsync());
        Assert.Equal("saved", listed.Name);
        Assert.Single(listed.Members);
        listed.Members[0].Role = ChannelRole.Admin;
        Assert.Equal(ChannelRole.Member, (await repository.GetAsync(channel.Id))!.Members[0].Role);
    }

    [Fact]
    public async Task StaleSnapshot_CannotOverwriteNewerSave()
    {
        var repository = new InMemoryChannelRepository();
        var channel = NewChannel();
        await repository.AddAsync(channel);
        var first = (await repository.GetAsync(channel.Id))!;
        var second = (await repository.GetAsync(channel.Id))!;
        first.Name = "winner";
        await repository.SaveAsync(first);
        second.Name = "stale";

        await Assert.ThrowsAsync<ChannelConcurrencyException>(() => repository.SaveAsync(second));
        Assert.Equal("winner", (await repository.GetAsync(channel.Id))!.Name);
        Assert.True(first.Version > second.Version);
    }

    [Fact]
    public async Task SaveAfterDelete_DoesNotResurrectChannel()
    {
        var repository = new InMemoryChannelRepository();
        var channel = NewChannel();
        await repository.AddAsync(channel);
        var read = (await repository.GetAsync(channel.Id))!;
        Assert.True(await repository.DeleteAsync(channel.Id));
        await Assert.ThrowsAsync<ChannelConcurrencyException>(() => repository.SaveAsync(read));
        Assert.Null(await repository.GetAsync(channel.Id));
    }

    private static Channel NewChannel() => new()
    {
        Id = Guid.NewGuid(), Name = "general", CreatedAt = DateTimeOffset.UtcNow
    };
}
