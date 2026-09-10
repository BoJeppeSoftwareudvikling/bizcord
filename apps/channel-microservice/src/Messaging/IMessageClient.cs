namespace ChannelService.Messaging;

// Holder applikationskoden uafhængig af EasyNetQ og RabbitMQ.
public interface IMessageClient
{
    Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    // Det returnerede handle kan disposes for at stoppe abonnementet.
    Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
