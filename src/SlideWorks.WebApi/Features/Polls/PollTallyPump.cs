using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// Turns tally changes into publishes (V5, D16): at most one IPollPublisher.PublishAsync per changed slide per deck in each
// half second, and never a change left unpublished. Its Changed handler only marks the deck's slide dirty and returns, so
// the answer writer never waits on a publish. The first change after a quiet half second is published at once; changes
// inside the half second after a publish wait for it to pass and go out together then, so the last change before a quiet
// period always goes out. A deck's window closes after a half second with nothing to publish.
//
// A publish that throws, a cancellation included, is logged and the pump carries on, unless the pump is stopping. It never
// publishes a poll event itself: PollPublisher does. TimeProvider drives the half second, so tests step it with a fake
// clock. On start it loads the open rounds of unended sessions into the tally before the app takes requests. On stop it
// drops its subscription and timers and waits for publishes already running.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class PollTallyPump(
    IPollTallies tallies,
    IPollPublisher publisher,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<PollTallyPump> logger) : IHostedService, IDisposable
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await PreloadAsync(cancellationToken);
        _subscription = tallies.Changed(OnChangedAsync);
    }

    // Loads the open rounds of audience sessions that have not ended: at most one session per deck is unended, so this is
    // bounded by the decks live when the app stopped. A round left open in an ended session is not loaded; it is answered
    // from its rows if anything reads it. A failure is logged and the app starts anyway: the answer writer reads a round
    // before it commits to it, so every round is loaded on demand.
    private async Task PreloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var open = await db.PollRounds.AsNoTracking()
                .Where(r => r.OpenedAt != null && r.ClosedAt == null)
                .Join(db.AudienceSessions.Where(s => s.EndedAt == null), r => r.SessionId, s => s.Id, (r, _) => r.Id)
                .ToListAsync(cancellationToken);
            foreach (var round in open)
            {
                await tallies.ReadAsync(round.ToString(), 0);
            }
            logger.LogInformation("Loaded {Count} open poll rounds into the tally", open.Count);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e, "Loading the open poll rounds into the tally failed; they load when first read");
        }
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

    // Marks the slide dirty. A deck with an open window is published when that window's half second passes; a deck with
    // none is published at once, on the thread pool rather than inside the writer's call. A Q&A round's change is not a
    // poll's: SW-9's own pump publishes it, so it is left alone here.
    private Task OnChangedAsync(PollRoundRef round)
    {
        lock (_gate)
        {
            if (_stopped || round.Kind != PollKind.Slide)
            {
                return Task.CompletedTask;
            }
            if (_windows.TryGetValue(round.DeckId, out var window))
            {
                window.Mark(round.Slide!.Value);
                return Task.CompletedTask;
            }
            window = new DeckWindow();
            window.Mark(round.Slide!.Value);
            _windows[round.DeckId] = window;
        }
        Track(Task.Run(() => RunAsync(round.DeckId)));
        return Task.CompletedTask;
    }

    // The window's half second has passed: publish what changed in it, or close the window when nothing did.
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
            logger.LogError(e, "The poll tally pump failed for deck {DeckId}", deckId);
        }
    }

    private async Task PumpAsync(string deckId)
    {
        while (true)
        {
            IReadOnlyList<int> slides;
            lock (_gate)
            {
                if (_stopped || !_windows.TryGetValue(deckId, out var window))
                {
                    return;
                }
                slides = window.Take();
                if (slides.Count == 0)
                {
                    _windows.Remove(deckId);
                    return;
                }
                window.PublishedAt = time.GetUtcNow();
            }
            foreach (var slide in slides)
            {
                await PublishAsync(deckId, slide);
            }
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

    private async Task PublishAsync(string deckId, int slide)
    {
        try
        {
            await publisher.PublishAsync(deckId, slide);
        }
        catch (Exception e) when (e is not OperationCanceledException || !IsStopped)
        {
            logger.LogError(e, "Publishing the poll on slide {Slide} of deck {DeckId} failed", slide, deckId);
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

    // A deck's half-second window: the slides changed since its last publish, when that publish started, and the timer
    // that ends the window.
    private sealed class DeckWindow
    {
        private readonly HashSet<int> _dirty = [];

        public DateTimeOffset PublishedAt { get; set; }

        public ITimer? Timer { get; set; }

        public void Mark(int slide) => _dirty.Add(slide);

        // The dirty slides in order, and none left dirty.
        public IReadOnlyList<int> Take()
        {
            int[] slides = [.. _dirty.Order()];
            _dirty.Clear();
            return slides;
        }
    }
}
