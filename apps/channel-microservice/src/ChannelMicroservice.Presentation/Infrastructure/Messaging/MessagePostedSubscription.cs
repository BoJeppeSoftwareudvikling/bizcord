using MessageClient;
using Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChannelService.Infrastructure.Messaging;

public sealed class MessagePostedSubscription(
    IMessageClient messageClient,
    IServiceScopeFactory scopeFactory)
    : IHostedService, IDisposable
{
    private const string SubscriptionId =
        "channel-service-message-posted";

    private readonly IMessageClient _messageClient =
        messageClient
        ?? throw new ArgumentNullException(nameof(messageClient));

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory
        ?? throw new ArgumentNullException(nameof(scopeFactory));

    private IDisposable? _subscription;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription =
            await _messageClient.SubscribeAsync<MessagePostedEvent>(
                SubscriptionId,
                HandleMessage,
                cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeSubscription();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeSubscription();
    }

    private async Task HandleMessage(
        MessagePostedEvent message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<MessagePostedHandler>();

        await handler.Handle(message, cancellationToken);
    }

    private void DisposeSubscription()
    {
        Interlocked
            .Exchange(ref _subscription, null)
            ?.Dispose();
    }
}
