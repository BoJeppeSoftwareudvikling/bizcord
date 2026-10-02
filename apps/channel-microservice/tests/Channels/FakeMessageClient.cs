using MessageClient;

namespace ChannelService.Tests.Channels;

public sealed class FakeMessageClient : IMessageClient
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }

    public TMessage? SinglePublished<TMessage>()
        where TMessage : class
    {
        return Published.OfType<TMessage>().SingleOrDefault();
    }

    public Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        return Task.FromResult<IDisposable>(new Subscription());
    }

    private sealed class Subscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
