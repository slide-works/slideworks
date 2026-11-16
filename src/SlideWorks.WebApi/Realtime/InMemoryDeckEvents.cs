using System.Collections.Immutable;
using System.Threading.Channels;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// One unbounded channel and one pump task per subscriber: Publish only enqueues, so it never waits for a handler.
// Disposing a subscription removes it before the next publish and stops its pump.
[RegisterSingleton(For = typeof(IDeckEvents))]
public sealed class InMemoryDeckEvents(ILogger<InMemoryDeckEvents> logger) : IDeckEvents, IDisposable
{
    private ImmutableList<Subscription> _subscriptions = [];

    public Task PublishAsync(DeckEvent deckEvent)
    {
        foreach (var subscription in _subscriptions.Where(s => s.Wants(deckEvent)))
        {
            subscription.Enqueue(deckEvent);
        }
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string? deckId, Func<DeckEvent, Task> handler)
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
        private readonly Channel<DeckEvent> _queue = Channel.CreateUnbounded<DeckEvent>();
        private readonly CancellationTokenSource _stop = new();
        private readonly Action<Subscription> _remove;
        private int _disposed;

        public Subscription(string? deckId, Func<DeckEvent, Task> handler, ILogger logger, Action<Subscription> remove)
        {
            _deckId = deckId;
            _remove = remove;
            _ = Task.Run(() => PumpAsync(handler, logger), _stop.Token);
        }

        public bool Wants(DeckEvent deckEvent) => _deckId == null || string.Equals(_deckId, deckEvent.DeckId, StringComparison.Ordinal);

        public void Enqueue(DeckEvent deckEvent) => _queue.Writer.TryWrite(deckEvent);

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

        private async Task PumpAsync(Func<DeckEvent, Task> handler, ILogger logger)
        {
            try
            {
                await foreach (var deckEvent in _queue.Reader.ReadAllAsync(_stop.Token))
                {
                    try
                    {
                        await handler(deckEvent);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogError(e, "A deck event handler threw for {EventType}", deckEvent.GetType().Name);
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
