using MessageClient;
using Messages;
using Microsoft.Extensions.Hosting;

namespace ChannelService.Application.Channels;

public sealed class MessagePostedSubscriber : IHostedService
{
    private readonly IMessageClient _messageClient;
    private readonly MessagePostedHandler _handler;
    private IDisposable? _subscription;

    public MessagePostedSubscriber(
        IMessageClient messageClient,
        MessagePostedHandler handler)
    {
        _messageClient = messageClient;
        _handler = handler;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = await _messageClient.SubscribeAsync<MessagePostedEvent>(
            "channel-service",
            _handler.Handle,
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
