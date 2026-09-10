using EasyNetQ;

namespace WebAPI.Messaging;

public sealed class EasyNetQMessageClient(IBus bus) : IMessageClient
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        return bus.PubSub.PublishAsync(message, cancellationToken);
    }

    public Task SubscribeAsync<TMessage>(string subscriptionId, Func<TMessage, Task> handler)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(handler);

        return bus.PubSub.SubscribeAsync(subscriptionId, handler);
    }
}
