using System.Collections.Immutable;
using System.Threading.Channels;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// One unbounded channel and one pump task per subscriber: Publish only enqueues, so it never waits for a handler.
// Disposing a subscription removes it before the next publish and stops its pump.
[RegisterSingleton(For = typeof(IAudienceEvents))]
public sealed class InMemoryAudienceEvents(ILogger<InMemoryAudienceEvents> logger) : IAudienceEvents, IDisposable
{
    private ImmutableList<Subscription> _subscriptions = [];

    public Task PublishAsync(AudienceEvent audienceEvent)
    {
        foreach (var subscription in _subscriptions.Where(s => s.Wants(audienceEvent)))
        {
            subscription.Enqueue(audienceEvent);
        }
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string? deckId, Func<AudienceEvent, Task> handler)
    {
        var subscription = new Subscription(deckId, handler, logger, Remove);
        ImmutableInterlocked.Update(ref _subscriptions, list => list.Add(subscription));
        return subscription;
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }

    private void Remove(Subscription subscription)
        => ImmutableInterlocked.Update(ref _subscriptions, list => list.Remove(subscription));

    private sealed class Subscription : IDisposable
    {
        private readonly string? _deckId;
        private readonly Channel<AudienceEvent> _queue = Channel.CreateUnbounded<AudienceEvent>();
        private readonly CancellationTokenSource _stop = new();
        private readonly Action<Subscription> _remove;
        private int _disposed;

        public Subscription(string? deckId, Func<AudienceEvent, Task> handler, ILogger logger, Action<Subscription> remove)
        {
            _deckId = deckId;
            _remove = remove;
            _ = Task.Run(() => PumpAsync(handler, logger), _stop.Token);
        }

        public bool Wants(AudienceEvent audienceEvent) => _deckId == null || string.Equals(_deckId, audienceEvent.DeckId, StringComparison.Ordinal);

        public void Enqueue(AudienceEvent audienceEvent) => _queue.Writer.TryWrite(audienceEvent);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            _remove(this);
            _stop.Cancel();
            _queue.Writer.TryComplete();
            _stop.Dispose();
        }

        private async Task PumpAsync(Func<AudienceEvent, Task> handler, ILogger logger)
        {
            try
            {
                await foreach (var audienceEvent in _queue.Reader.ReadAllAsync(_stop.Token))
                {
                    try
                    {
                        await handler(audienceEvent);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogError(e, "An audience event handler threw for {EventType}", audienceEvent.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed: stop delivering.
            }
        }
    }
}
