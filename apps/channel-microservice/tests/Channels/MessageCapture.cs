using MessageClient;

namespace ChannelService.Tests.Channels;

public sealed class MessageCapture<TMessage> : IDisposable
    where TMessage : class
{
    private readonly IDisposable _subscription;
    private readonly TaskCompletionSource<TMessage> _received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MessageCapture(IMessageClient client)
    {
        _subscription = client.SubscribeAsync<TMessage>(
            $"capture-{typeof(TMessage).Name}",
            (message, _) =>
            {
                _received.TrySetResult(message);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
    }

    public async Task<TMessage> WaitForMessageAsync(TimeSpan timeout)
    {
        var completed = await Task.WhenAny(_received.Task, Task.Delay(timeout));

        if (completed != _received.Task)
        {
            throw new TimeoutException(
                $"No {typeof(TMessage).Name} arrived within {timeout.TotalSeconds} seconds.");
        }

        return await _received.Task;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
