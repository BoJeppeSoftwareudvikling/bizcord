using MessageClient;
using EasyNetQ;
using EasyNetQ.Internals;
using EasyNetQ.Topology;
using Moq;

namespace ChannelService.Tests.Messaging;

public sealed class EasyNetQMessageClientTests
{
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IPubSub> _pubSub = new();

    public EasyNetQMessageClientTests()
    {
        _bus
            .SetupGet(candidate => candidate.PubSub)
            .Returns(_pubSub.Object);
    }

    [Fact]
    public async Task PublishAsync_DelegatesMessageAndCancellationToken()
    {
        var message = new TestMessage("Hello");
        using var cancellationTokenSource = new CancellationTokenSource();
        var client = new EasyNetQMessageClient(_bus.Object);

        await client.PublishAsync(message, cancellationTokenSource.Token);

        _pubSub.Verify(
            candidate => candidate.PublishAsync(
                message,
                It.IsAny<Action<IPublishConfiguration>>(),
                cancellationTokenSource.Token),
            Times.Once);
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
            .Setup(candidate => candidate.SubscribeAsync<TestMessage>(
                subscriptionId,
                It.IsAny<Func<TestMessage, CancellationToken, Task>>(),
                It.IsAny<Action<ISubscriptionConfiguration>>(),
                cancellationTokenSource.Token))
            .Returns(awaitableSubscription);
        var client = new EasyNetQMessageClient(_bus.Object);

        var result = await client.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationTokenSource.Token);

        Assert.Equal(subscription, result);
        _pubSub.Verify(
            candidate => candidate.SubscribeAsync<TestMessage>(
                subscriptionId,
                It.Is<Func<TestMessage, CancellationToken, Task>>(
                    candidateHandler => candidateHandler == handler),
                It.IsAny<Action<ISubscriptionConfiguration>>(),
                cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_RejectsNullMessage()
    {
        var client = new EasyNetQMessageClient(_bus.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.PublishAsync<TestMessage>(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SubscribeAsync_RejectsInvalidSubscriptionId(string subscriptionId)
    {
        var client = new EasyNetQMessageClient(_bus.Object);

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

        return new SubscriptionResult(
            exchange,
            queue,
            new Mock<IDisposable>().Object);
    }

    private sealed record TestMessage(string Text);
}
