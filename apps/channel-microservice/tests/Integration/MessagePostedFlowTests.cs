using ChannelService.Application;
using ChannelService.Application.Abstractions;
using ChannelService.Domain;
using ChannelService.Infrastructure;
using MessageClient;
using Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using UploadWorkflow;

namespace ChannelService.Tests.Integration;

public sealed class MessagePostedFlowTests : IAsyncLifetime
{
    private const string RabbitMqUser = "bizcord";
    private const string RabbitMqPassword = "bizcord";

    private readonly RabbitMqContainer _rabbitMqContainer =
        new RabbitMqBuilder("rabbitmq:3-management")
            .WithUsername(RabbitMqUser)
            .WithPassword(RabbitMqPassword)
            .Build();

    public Task InitializeAsync()
    {
        return _rabbitMqContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MessagePosted_IsConsumed_AndActivityEventIsPublished()
    {
        using var host = CreateChannelServiceHost();

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var repository = host.Services
            .GetRequiredService<IChannelRepository>();

        await repository.AddAsync(channel);

        await host.StartAsync();

        try
        {
            var messageClient = host.Services
                .GetRequiredService<IMessageClient>();
            Assert.IsType<BoundedMessageClient>(messageClient);

            var capturedEvent =
                new TaskCompletionSource<ChannelActivityRecordedEventDto>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            using var captureSubscription =
                await messageClient
                    .SubscribeAsync<ChannelActivityRecordedEventDto>(
                        $"channel-activity-test-{Guid.NewGuid():N}",
                        (message, _) =>
                        {
                            capturedEvent.TrySetResult(message);
                            return Task.CompletedTask;
                        });

            var messageId = Guid.NewGuid();
            var postedAt = DateTime.UtcNow;

            await messageClient.PublishAsync(
                new MessagePostedEvent
                {
                    MessageId = messageId,
                    ChannelId = channel.Id,
                    AuthorId = Guid.NewGuid(),
                    Content = "Hello world",
                    PostedAt = postedAt
                });

            var result = await capturedEvent.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            Assert.Equal(messageId, result.MessageId);
            Assert.Equal(channel.Id, result.ChannelId);
            Assert.Equal(
                new DateTimeOffset(postedAt),
                result.ActivityAt);

            var storedChannel = await repository.GetAsync(channel.Id);

            Assert.NotNull(storedChannel);
            Assert.Equal(messageId, storedChannel.LastMessageId);
            Assert.Equal(
                new DateTimeOffset(postedAt),
                storedChannel.LastActivityAt);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Theory]
    [InlineData(1024, UploadStep.Completed)]
    [InlineData(1_048_577, UploadStep.Rejected)]
    [Trait("Category", "Integration")]
    public async Task AttachmentUpload_ReachesExpectedOutcomeOverRabbitMq(long bytes, UploadStep expected)
    {
        using var host = CreateChannelServiceHost();
        var channel = new Channel { Id = Guid.NewGuid(), Name = "general" };
        channel.Members.Add(new ChannelMember { UserId = Guid.NewGuid(), ChannelId = channel.Id });
        var repository = host.Services.GetRequiredService<IChannelRepository>();
        await repository.AddAsync(channel);
        var directory = Directory.CreateTempSubdirectory("bizcord-upload");
        await host.StartAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var ct = timeout.Token;
            var bus = host.Services.GetRequiredService<IMessageClient>();
            var coordinator = new UploadCoordinator(Path.Combine(directory.FullName, "saga.db"), bus);
            var participants = new SimulatedParticipants(directory.FullName, bus);
            using var files = await bus.SubscribeAsync<FileCommand>("test-files", participants.Handle, ct);
            using var messages = await bus.SubscribeAsync<AttachmentMessageCommand>("test-messages", participants.Handle, ct);
            using var quota = await bus.SubscribeAsync<QuotaCommand>("test-quota", participants.Handle, ct);
            using var starts = await bus.SubscribeAsync<StartUpload>("test-start", coordinator.Start, ct);
            using var results = await bus.SubscribeAsync<UploadStepResult>("test-results", coordinator.Receive, ct);
            var upload = new StartUpload(Guid.NewGuid(), Guid.NewGuid(), channel.Id, bytes, DateTimeOffset.UtcNow);
            await bus.PublishAsync(upload, ct);
            while (coordinator.Status(upload.SagaId)?.Step is not (UploadStep.Completed or UploadStep.Rejected))
            {
                await coordinator.DispatchPending(ct);
                await Task.Delay(100, ct);
            }

            Assert.Equal(expected, coordinator.Status(upload.SagaId)!.Value.Step);
            var stored = await repository.GetAsync(channel.Id, ct);
            Assert.Equal(expected == UploadStep.Completed ? upload.SagaId : (Guid?)null, stored!.LastMessageId);
            Assert.Equal(expected == UploadStep.Completed ? 1 : 0, stored.Members.Single().UnreadAttachments);
        }
        finally
        {
            await host.StopAsync();
            directory.Delete(true);
        }
    }

    private IHost CreateChannelServiceHost()
    {
        var connectionString =
            $"host={_rabbitMqContainer.Hostname};" +
            $"port={_rabbitMqContainer.GetMappedPublicPort(5672)};" +
            $"username={RabbitMqUser};" +
            $"password={RabbitMqPassword}";

        return Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Messaging:ConnectionString"] =
                                connectionString
                        });
                })
            .ConfigureServices(
                (context, services) =>
                {
                    services.AddMessageClient(context.Configuration);
                    services.AddChannelApplication();
                    services.AddChannelInfrastructure();
                })
            .Build();
    }
}
