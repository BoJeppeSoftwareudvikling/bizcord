using ChannelService.Application.Channels;
using ChannelService.Domain;
using ChannelService.Infrastructure;
using MessageClient;
using Messages;
using Microsoft.Extensions.Options;
using Moq;

namespace ChannelService.Tests.Application.Channels;

public sealed class ChannelReliabilityTests
{
    [Fact]
    public async Task InvalidDescription_DoesNotPartiallyChangeNameOrPublish()
    {
        var repository = new InMemoryChannelRepository();
        var channel = new Channel { Id = Guid.NewGuid(), Name = "original" };
        await repository.AddAsync(channel);
        var client = new Mock<IMessageClient>(MockBehavior.Strict);
        var service = new ChannelManagementService(repository, client.Object);

        await Assert.ThrowsAsync<ChannelValidationException>(() => service.UpdateAsync(channel.Id,
            new UpdateChannelCommand("new name", new string('x', 501))));

        var stored = (await repository.GetAsync(channel.Id))!;
        Assert.Equal("original", stored.Name);
        Assert.Null(stored.UpdatedAt);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OlderMessage_DoesNotMoveLastActivityBackwardsOrPublish()
    {
        var repository = new InMemoryChannelRepository();
        var newestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var channel = new Channel
        {
            Id = Guid.NewGuid(), Name = "general", LastMessageId = newestId, LastActivityAt = now
        };
        await repository.AddAsync(channel);
        var client = new Mock<IMessageClient>(MockBehavior.Strict);
        var service = new ChannelManagementService(repository, client.Object);

        await service.RecordMessageActivityAsync(channel.Id, Guid.NewGuid(), now.AddMinutes(-1));

        var stored = (await repository.GetAsync(channel.Id))!;
        Assert.Equal(now, stored.LastActivityAt);
        Assert.Equal(newestId, stored.LastMessageId);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadsRemainAvailableWhilePublisherIsStalled()
    {
        var inner = new Mock<IMessageClient>();
        inner.Setup(client => client.PublishAsync(It.IsAny<ChannelCreatedEventDto>(),
                It.IsAny<CancellationToken>()))
            .Returns((ChannelCreatedEventDto _, CancellationToken token) => Task.Delay(Timeout.Infinite, token));
        var client = new BoundedMessageClient(inner.Object, Options.Create(new MessagingOptions
        {
            PublishTimeout = TimeSpan.FromMilliseconds(100), MaxConcurrentPublishes = 1
        }));
        var service = new ChannelManagementService(new InMemoryChannelRepository(), client);
        var write = service.CreateAsync(new CreateChannelCommand("general", null));

        var channels = await service.ListAsync();
        Assert.Equal("general", Assert.Single(channels).Name);
        await Assert.ThrowsAsync<PublishTimeoutException>(() => write.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Single(await service.ListAsync());
    }
}
