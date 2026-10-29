using Microsoft.Extensions.Options;

namespace MessageClient;

// Afgrænser ventetid og samtidige brokerkald for alle publishers i processen.
public sealed class BoundedMessageClient : IMessageClient
{
    private readonly IMessageClient _inner;
    private readonly TimeSpan _publishTimeout;
    private readonly SemaphoreSlim _slots;

    public BoundedMessageClient(IMessageClient inner, IOptions<MessagingOptions> options)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        if (settings.PublishTimeout <= TimeSpan.Zero ||
            settings.PublishTimeout > TimeSpan.FromMinutes(5) ||
            settings.MaxConcurrentPublishes is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid publish limits.");
        }

        _publishTimeout = settings.PublishTimeout;
        _slots = new SemaphoreSlim(settings.MaxConcurrentPublishes, settings.MaxConcurrentPublishes);
    }

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_slots.Wait(0))
        {
            throw new MessagingCapacityExceededException();
        }

        var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? pending = null;
        try
        {
            budget.CancelAfter(_publishTimeout);
            pending = _inner.PublishAsync(message, budget.Token);
            await pending.WaitAsync(budget.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested)
        {
            throw new PublishTimeoutException(exception);
        }
        finally
        {
            if (pending is { IsCompleted: false })
            {
                // Et kald, der ignorerer cancellation, optager plads indtil det faktisk afsluttes.
                _ = pending.ContinueWith(completed =>
                {
                    _ = completed.Exception;
                    budget.Dispose();
                    _slots.Release();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            else
            {
                _ = pending?.Exception;
                budget.Dispose();
                _slots.Release();
            }
        }
    }

    public Task<IDisposable> SubscribeAsync<TMessage>(string subscriptionId,
        Func<TMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        where TMessage : class => _inner.SubscribeAsync(subscriptionId, handler, cancellationToken);
}
