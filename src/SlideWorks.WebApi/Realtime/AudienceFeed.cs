using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Realtime.Presenting;
using System.Collections.Concurrent;

namespace SlideWorks.WebApi.Realtime;

// The state behind IAudienceRelay and the presentation observer: one instance, so both share one seq and one ordering.
// Carries the presenter's position to the guests (J4). It follows SW-4's presentation state (IPresentationObserver) and
// the deck's own changes (IDeckEvents), and publishes an audienceState for every one that lands on a deck with an open
// session. The phase always comes from IAudienceSessions and the one slide on screen is projected with no notes and no
// deep-dive mark (D15); Current is filled only while the phase is live.
//
// It is a singleton because it is an observer, so IAudienceSessions and the database are reached through a scope per call.
// It never reads the request or the caller. IPresentations is resolved per call too, because the registry takes the
// observers in its own constructor.
//
// Following is derived, not remembered: any change for a deck whose session is open is published, so a restart with a
// session still open in the database leaves guests fed from the first change after it. Started is safe to call again.
// Seq is seeded from the clock the first time a deck publishes after a start, so it also keeps rising across a restart.
[RegisterSingleton]
public sealed class AudienceFeed : IDisposable
{
    private const string JOIN_LAYOUT = "join";

    private readonly IServiceScopeFactory _scopes;
    private readonly IAudienceEvents _audienceEvents;
    private readonly TimeProvider _clock;
    private readonly IDisposable _deckSubscription;
    private readonly ConcurrentDictionary<string, DeckFeed> _feeds = new(StringComparer.Ordinal);

    public AudienceFeed(IServiceScopeFactory scopes, IAudienceEvents audienceEvents, IDeckEvents deckEvents, TimeProvider clock)
    {
        _scopes = scopes;
        _audienceEvents = audienceEvents;
        _clock = clock;
        _deckSubscription = deckEvents.Subscribe(null, OnDeckEventAsync);
    }

    public void Dispose() => _deckSubscription.Dispose();

    public async Task<AudienceState?> CurrentAsync(string deckId)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().ForDeckAsync(deckId);
        if (found is null)
        {
            return null;
        }
        // What a guest is handed on joining carries the last seq published, so a push that follows is always higher.
        return await ComposeAsync(scope.ServiceProvider, found, Feed(deckId).Latest, restoreIfLost: true);
    }

    public async Task StartedAsync(string deckId)
    {
        await using var scope = _scopes.CreateAsyncScope();
        // Idempotent: brings back a presentation a restart dropped, and publishes the state that turns lobby guests live.
        await scope.ServiceProvider.GetRequiredService<IPresentations>().StartAsync(deckId);
        await PublishAsync(scope.ServiceProvider, deckId);
    }

    public Task EndedAsync(string deckId)
    {
        _feeds.TryRemove(deckId, out _);
        return _audienceEvents.PublishAsync(new AudienceEnded(deckId));
    }

    public async Task OnChangedAsync(PresentationState state)
    {
        await using var scope = _scopes.CreateAsyncScope();
        await PublishAsync(scope.ServiceProvider, state.DeckId, state);
    }

    private async Task OnDeckEventAsync(DeckEvent deckEvent)
    {
        if (deckEvent is not DeckChanged)
        {
            return;
        }
        await using var scope = _scopes.CreateAsyncScope();
        await PublishAsync(scope.ServiceProvider, deckEvent.DeckId);
    }

    // Publishes the deck's state when its session is open, and nothing otherwise (a lobby or an ended deck has no position).
    private async Task PublishAsync(IServiceProvider services, string deckId, PresentationState? presentation = null)
    {
        var feed = Feed(deckId);
        await feed.Gate.WaitAsync();
        try
        {
            var found = await services.GetRequiredService<IAudienceSessions>().ForDeckAsync(deckId);
            if (found is not { Phase: AudiencePhase.Live, SessionId: { } sessionId })
            {
                return;
            }
            var state = await ComposeAsync(services, found, feed.Next(_clock), restoreIfLost: false, presentation);
            await StampJoinSlideAsync(services, sessionId, state);
            await _audienceEvents.PublishAsync(state);
        }
        finally
        {
            feed.Gate.Release();
        }
    }

    private static async Task<AudienceState> ComposeAsync(
        IServiceProvider services,
        AudienceSessionRef found,
        long seq,
        bool restoreIfLost,
        PresentationState? presentation = null)
    {
        if (found.Phase != AudiencePhase.Live)
        {
            return new AudienceState(found.DeckId, seq, found.Phase, null, 0, null);
        }
        var presentations = services.GetRequiredService<IPresentations>();
        presentation ??= await presentations.GetAsync(found.DeckId);
        if (presentation is null && restoreIfLost)
        {
            // The registry is in memory, so a restart drops it while the session stays open: start it again, as Go live does.
            presentation = await presentations.StartAsync(found.DeckId);
        }
        var db = services.GetRequiredService<AppDbContext>();
        var revision = await db.Decks.AsNoTracking().Where(d => d.Id == found.DeckId).Select(d => d.Revision).SingleAsync();
        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == found.DeckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
        return new AudienceState(found.DeckId, seq, AudiencePhase.Live, Project(slides, presentation), revision, null);
    }

    // The one slide on screen. A position past the end of the deck (slides removed while live) shows the last slide.
    private static AudienceSlide? Project(List<Slide> slides, PresentationState? presentation)
    {
        if (presentation is null || slides.Count == 0)
        {
            return null;
        }
        var index = Math.Clamp(presentation.Slide, 1, slides.Count);
        var slide = slides[index - 1];
        var step = Math.Clamp(presentation.Step, 0, slide.Steps);
        return new AudienceSlide(index, slides.Count, slide.Layout, SlideJson.Slots(slide), slide.Steps, step, presentation.Black);
    }

    // J8: the first time the live state lands on a join slide. A conditional update, so a repeat (or a second process)
    // leaves the first stamp alone.
    private async Task StampJoinSlideAsync(IServiceProvider services, string sessionId, AudienceState state)
    {
        if (state.Current is not { Layout: JOIN_LAYOUT })
        {
            return;
        }
        var id = Guid.Parse(sessionId);
        var now = _clock.GetUtcNow();
        await services.GetRequiredService<AppDbContext>().AudienceSessions
            .Where(s => s.Id == id && s.JoinSlideFirstShownAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.JoinSlideFirstShownAt, now));
    }

    private DeckFeed Feed(string deckId) => _feeds.GetOrAdd(deckId, _ => new DeckFeed());

    // One deck's ordering: publishes go one at a time, so seq never goes backwards on the wire.
    private sealed class DeckFeed
    {
        private long _seq;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public long Latest => Interlocked.Read(ref _seq);

        public long Next(TimeProvider clock)
        {
            var floor = clock.GetUtcNow().ToUnixTimeMilliseconds();
            long current;
            long next;
            do
            {
                current = Interlocked.Read(ref _seq);
                next = Math.Max(current + 1, floor);
            }
            while (Interlocked.CompareExchange(ref _seq, next, current) != current);
            return next;
        }
    }
}
