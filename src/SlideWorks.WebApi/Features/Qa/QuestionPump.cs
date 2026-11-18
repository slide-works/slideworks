using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// Turns upvote changes into publishes (Q5): at most one IQuestionPublisher.PublishAsync per deck in each half second, and
// never a change left unpublished. It takes only the tally's changes on a Q&A round; a slide poll's are SW-6's pump's. Its
// Changed handler only marks the deck dirty and returns, so the answer writer never waits on a publish and never sees one
// fail. The first change after a quiet half second is published at once, on the thread pool; changes inside the half second
// after a publish wait for it to pass and go out together then, so the last change before a quiet period always goes out.
// A deck's window closes after a half second with nothing to publish.
//
// It never reads the tally or builds an event: the publisher reads the presenter's view, which counts each round's votes
// over its current questions, and leaves out what another guest may not see. A publish that throws, a cancellation
// included, is logged and the pump carries on, unless the pump is stopping. TimeProvider drives the half second, so tests
// step it with a fake clock. Nothing to load on start: SW-6's pump loads every open round into the tally, Q&A rounds
// included. On stop it drops its subscription and timers and waits for publishes already running. A singleton.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class QuestionPump(
    IPollTallies tallies,
    IQuestionPublisher publisher,
    TimeProvider time,
    ILogger<QuestionPump> logger) : IHostedService, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DeckWindow> _windows = new(StringComparer.Ordinal);
    private readonly HashSet<Task> _running = [];
    private IDisposable? _subscription;
    private bool _stopped;

    private bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return _stopped;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = tallies.Changed(OnChangedAsync);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task[] running;
        lock (_gate)
        {
            running = [.. _running];
        }
        Dispose();
        // When the host stops waiting, the publishes still running end on their own.
        await Task.WhenAny(Task.WhenAll(running), Task.Delay(Timeout.InfiniteTimeSpan, time, cancellationToken));
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        lock (_gate)
        {
            _stopped = true;
            foreach (var window in _windows.Values)
            {
                window.Timer?.Dispose();
            }
            _windows.Clear();
        }
    }

    // Marks the deck dirty. A deck with an open window is published when that window's half second passes; a deck with none
    // is published at once, on the thread pool rather than inside the writer's call. A slide poll's change is left alone.
    private Task OnChangedAsync(PollRoundRef round)
    {
        lock (_gate)
        {
            if (_stopped || round.Kind != PollKind.Qa)
            {
                return Task.CompletedTask;
            }
            if (_windows.TryGetValue(round.DeckId, out var window))
            {
                window.Dirty = true;
                return Task.CompletedTask;
            }
            _windows[round.DeckId] = new DeckWindow { Dirty = true };
        }
        Track(Task.Run(() => RunAsync(round.DeckId)));
        return Task.CompletedTask;
    }

    // The window's half second has passed: publish if anything changed in it, or close the window when nothing did.
    private void OnTick(string deckId)
    {
        lock (_gate)
        {
            if (_stopped || !_windows.TryGetValue(deckId, out var window))
            {
                return;
            }
            window.Timer?.Dispose();
            window.Timer = null;
        }
        Track(RunAsync(deckId));
    }

    private async Task RunAsync(string deckId)
    {
        try
        {
            await PumpAsync(deckId);
        }
        catch (Exception e) when (e is not OperationCanceledException || !IsStopped)
        {
            logger.LogError(e, "The question vote pump failed for deck {DeckId}", deckId);
        }
    }

    private async Task PumpAsync(string deckId)
    {
        while (true)
        {
            lock (_gate)
            {
                if (_stopped || !_windows.TryGetValue(deckId, out var window))
                {
                    return;
                }
                if (!window.Dirty)
                {
                    _windows.Remove(deckId);
                    return;
                }
                window.Dirty = false;
                window.PublishedAt = time.GetUtcNow();
            }
            await PublishAsync(deckId);
            lock (_gate)
            {
                if (_stopped || !_windows.TryGetValue(deckId, out var window))
                {
                    return;
                }
                var wait = window.PublishedAt + Interval - time.GetUtcNow();
                if (wait > TimeSpan.Zero)
                {
                    window.Timer = time.CreateTimer(state => OnTick((string)state!), deckId, wait, Timeout.InfiniteTimeSpan);
                    return;
                }
            }
        }
    }

    private async Task PublishAsync(string deckId)
    {
        try
        {
            await publisher.PublishAsync(deckId);
        }
        catch (Exception e) when (e is not OperationCanceledException || !IsStopped)
        {
            logger.LogError(e, "Publishing the questions of deck {DeckId} failed", deckId);
        }
    }

    // Keeps the pumps that are running, for StopAsync to wait on; finished ones are dropped as new ones start.
    private void Track(Task pumping)
    {
        lock (_gate)
        {
            _running.RemoveWhere(t => t.IsCompleted);
            _running.Add(pumping);
        }
    }

    // A deck's half-second window: whether anything changed since its last publish, when that publish started, and the
    // timer that ends the window.
    private sealed class DeckWindow
    {
        public bool Dirty { get; set; }

        public DateTimeOffset PublishedAt { get; set; }

        public ITimer? Timer { get; set; }
    }
}
