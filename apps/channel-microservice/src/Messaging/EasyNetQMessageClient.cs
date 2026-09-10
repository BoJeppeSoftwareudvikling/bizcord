using EasyNetQ;

namespace ChannelService.Messaging;

// Oversætter IMessageClient-kald til EasyNetQ's Pub/Sub-API.
public sealed class EasyNetQMessageClient(IBus bus) : IMessageClient
{
    private readonly IBus _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    public Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        return _bus.PubSub.PublishAsync(message, cancellationToken: cancellationToken);
    }

    public async Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(handler);

        return await _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            // EasyNetQ kræver en konfigurationsfunktion; standardindstillingerne er nok her.
            _ => { },
            cancellationToken: cancellationToken);
    }
}
