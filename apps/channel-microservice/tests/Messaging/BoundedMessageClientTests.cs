using MessageClient;
using Microsoft.Extensions.Options;

namespace ChannelService.Tests.Messaging;

public sealed class BoundedMessageClientTests
{
    [Fact]
    public async Task PublishTimeout_CancelsBrokerCall_WithoutRetry()
    {
        CancellationToken observedToken = default;
        var inner = new StubClient(token =>
        {
            observedToken = token;
            return Task.Delay(Timeout.Infinite, token);
        });
        var client = Create(inner, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<PublishTimeoutException>(() =>
            client.PublishAsync("event").WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(observedToken.IsCancellationRequested);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Bulkhead_RejectsExcessCalls_AndRecoversAfterCompletion()
    {
        var gate = new TaskCompletionSource();
        var inner = new StubClient(_ => gate.Task);
        var client = Create(inner);
        var first = client.PublishAsync("first");

        await Assert.ThrowsAsync<MessagingCapacityExceededException>(() => client.PublishAsync("second"));
        Assert.Equal(1, inner.Calls);

        gate.SetResult();
        await first;
        await client.PublishAsync("third");
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task IgnoredCancellation_DoesNotReleaseSlotUntilBrokerTaskFinishes()
    {
        var gate = new TaskCompletionSource();
        var inner = new StubClient(_ => gate.Task);
        var client = Create(inner, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<PublishTimeoutException>(() =>
            client.PublishAsync("first").WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<MessagingCapacityExceededException>(() => client.PublishAsync("second"));

        gate.SetException(new IOException("Late broker failure"));
        inner.Publish = _ => Task.CompletedTask;
        await client.PublishAsync("third");
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToTimeout_AndReleasesSlot()
    {
        var inner = new StubClient(token => Task.Delay(Timeout.Infinite, token));
        var client = Create(inner);
        using var cancellation = new CancellationTokenSource();
        var pending = client.PublishAsync("event", cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        inner.Publish = _ => Task.CompletedTask;
        await client.PublishAsync("next");
    }

    [Fact]
    public async Task AlreadyCancelledRequest_DoesNotCallBroker()
    {
        var inner = new StubClient(_ => Task.CompletedTask);
        var client = Create(inner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.PublishAsync("event", new CancellationToken(true)));
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task BrokerException_IsPropagatedOnce_AndReleasesSlot()
    {
        var failure = new IOException("Connection lost");
        var inner = new StubClient(_ => throw failure);
        var client = Create(inner);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => client.PublishAsync("event")));
        Assert.Equal(1, inner.Calls);
        inner.Publish = _ => Task.CompletedTask;
        await client.PublishAsync("next");
    }

    private static BoundedMessageClient Create(StubClient inner, TimeSpan? timeout = null) =>
        new(inner, Options.Create(new MessagingOptions
        {
            PublishTimeout = timeout ?? TimeSpan.FromSeconds(5),
            MaxConcurrentPublishes = 1
        }));

    private sealed class StubClient(Func<CancellationToken, Task> publish) : IMessageClient
    {
        public Func<CancellationToken, Task> Publish { get; set; } = publish;
        public int Calls { get; private set; }

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class
        {
            Calls++;
            return Publish(cancellationToken);
        }

        public Task<IDisposable> SubscribeAsync<TMessage>(string subscriptionId,
            Func<TMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
            where TMessage : class => throw new NotSupportedException();
    }
}
