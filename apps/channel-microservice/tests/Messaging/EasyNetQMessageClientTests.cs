using ChannelService.Messaging;
using EasyNetQ;
using EasyNetQ.Internals;
using EasyNetQ.Topology;
using NSubstitute;

namespace ChannelService.Tests.Messaging;

public sealed class EasyNetQMessageClientTests
{
    private readonly IBus _bus = Substitute.For<IBus>();
    private readonly IPubSub _pubSub = Substitute.For<IPubSub>();

    public EasyNetQMessageClientTests()
    {
        _bus.PubSub.Returns(_pubSub);
    }

    [Fact]
    public async Task PublishAsync_DelegatesMessageAndCancellationToken()
    {
        var message = new TestMessage("Hello");
        using var cancellationTokenSource = new CancellationTokenSource();
        var client = new EasyNetQMessageClient(_bus);

        await client.PublishAsync(message, cancellationTokenSource.Token);

        await _pubSub.Received(1).PublishAsync(
            message,
            Arg.Any<Action<IPublishConfiguration>>(),
            cancellationTokenSource.Token);
    }

    [Fact]
    public async Task SubscribeAsync_DelegatesSubscriptionAndReturnsHandle()
    {
        const string subscriptionId = "channel-service";
        using var cancellationTokenSource = new CancellationTokenSource();
        Func<TestMessage, CancellationToken, Task> handler = (_, _) => Task.CompletedTask;
        var subscription = CreateSubscriptionResult();
        var awaitableSubscription = new AwaitableDisposable<SubscriptionResult>(
            Task.FromResult(subscription));
        _pubSub
            .SubscribeAsync<TestMessage>(
                subscriptionId,
                Arg.Any<Func<TestMessage, CancellationToken, Task>>(),
                Arg.Any<Action<ISubscriptionConfiguration>>(),
                cancellationTokenSource.Token)
            .Returns(awaitableSubscription);
        var client = new EasyNetQMessageClient(_bus);

        var result = await client.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationTokenSource.Token);

        Assert.Equal(subscription, result);
        _ = _pubSub.Received(1).SubscribeAsync<TestMessage>(
            subscriptionId,
            Arg.Is<Func<TestMessage, CancellationToken, Task>>(candidate => candidate == handler),
            Arg.Any<Action<ISubscriptionConfiguration>>(),
            cancellationTokenSource.Token);
    }

    [Fact]
    public async Task PublishAsync_RejectsNullMessage()
    {
        var client = new EasyNetQMessageClient(_bus);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.PublishAsync<TestMessage>(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SubscribeAsync_RejectsInvalidSubscriptionId(string subscriptionId)
    {
        var client = new EasyNetQMessageClient(_bus);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SubscribeAsync<TestMessage>(
                subscriptionId,
                (_, _) => Task.CompletedTask));
    }

    private static SubscriptionResult CreateSubscriptionResult()
    {
        var exchange = new Exchange(
            "test-exchange",
            "topic",
            durable: false,
            autoDelete: true,
            new Dictionary<string, object>());
        var queue = new Queue(
            "test-queue",
            false,
            true,
            true,
            new Dictionary<string, object>());

        return new SubscriptionResult(exchange, queue, Substitute.For<IDisposable>());
    }

    private sealed record TestMessage(string Text);
}
