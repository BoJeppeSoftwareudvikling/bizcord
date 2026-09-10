namespace WebAPI.Messaging;

public interface IMessageClient
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;

    Task SubscribeAsync<TMessage>(string subscriptionId, Func<TMessage, Task> handler)
        where TMessage : class;
}
