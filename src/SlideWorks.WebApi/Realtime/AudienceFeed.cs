using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Realtime.Presenting;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SlideWorks.WebApi.Realtime;

// The state behind IAudienceRelay and the presentation observer: one instance, so both share one seq and one ordering.
// Carries the presenter's position to the guests (J4). It follows SW-4's presentation state (IPresentationObserver) and
// the deck's own changes (IDeckEvents), and publishes an audienceState for every one that lands on a deck with an open
// session. The phase always comes from IAudienceSessions and the one slide on screen is projected with no notes and no
// deep-dive mark (D15); Current and Activity are filled only while the phase is live. Activity is what IAudienceActivity
// says is on screen (a poll, V3), serialised as it goes to the guest.
//
// It is a singleton because it is an observer, so IAudienceSessions and the database are reached through a scope per call.
// It never reads the request or the caller. IPresentations is resolved per call too, because the registry takes the
// observers in its own constructor.
//
// Following is derived, not remembered: any change for a deck whose session is open is published, so a restart with a
// session still open in the database leaves guests fed from the first change after it. Started is safe to call again.
// Nothing reached by a guest starts a presentation: a live session with none running is live with no current slide until
// the presenter's rejoin brings it back. Seq comes from one source, floored to the clock, so it keeps rising across a restart and the lobby-live-ended cycle; an
// answer takes its seq before it reads the phase. A deck has
// an entry (its gate and seq) only while its session is open; the entry goes when the session ends.
[RegisterSingleton]
public sealed class AudienceFeed : IDisposable
{
    private const string JOIN_LAYOUT = "join";

    private readonly IServiceScopeFactory _scopes;
    private readonly IAudienceEvents _audienceEvents;
    private readonly TimeProvider _clock;
    private readonly IDisposable _deckSubscription;
    private long _lastSeq;
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
        // Taken before the phase is read, so an answer built from an older read always carries an older seq than any push
        // that read could have missed (a lobby read that began before Go live, answered after the first live push).
        var early = NextSeq();
        var (feed, found) = await EnterAsync(scope.ServiceProvider, deckId);
        if (feed is null)
        {
            // A lobby or an ended deck has no position to order and gets no entry, but its answer still comes from the one seq source.
            return found is null ? null : await ComposeAsync(scope.ServiceProvider, found, early);
        }
        try
        {
            // A live answer takes its seq again under the gate, from the same source as pushed events, so every push that
            // follows is higher; its phase was read inside the gate, so it can't be older than a push that came before.
            // Reached by anonymous callers: it reads SW-4's state and never starts a presentation. A live session with no
            // presentation (a restart dropped it) is live with no current slide until the presenter's rejoin notifies us.
            return await ComposeAsync(scope.ServiceProvider, found!, NextSeq());
        }
        finally
        {
            feed.Gate.Release();
        }
    }

    public async Task StartedAsync(string deckId)
    {
        await using var scope = _scopes.CreateAsyncScope();
        // Idempotent. Only the owner's Go live gets here, so it may start a presentation when none is running; this is
        // also what brings one back for a restart followed by a new Go live. It publishes the state that turns lobby guests live.
        await scope.ServiceProvider.GetRequiredService<IPresentations>().StartAsync(deckId);
        await PublishAsync(scope.ServiceProvider, deckId);
    }

    public async Task EndedAsync(string deckId)
    {
        if (!_feeds.TryGetValue(deckId, out var feed))
        {
            await _audienceEvents.PublishAsync(new AudienceEnded(deckId));
            return;
        }
        // Behind any publish in flight, which may still have read the phase as live: audienceEnded is the last event.
        await feed.Gate.WaitAsync();
        try
        {
            await _audienceEvents.PublishAsync(new AudienceEnded(deckId));
            Close(deckId, feed);
        }
        finally
        {
            feed.Gate.Release();
        }
    }

    // Recomposes and publishes the deck's state, behind its gate like every other publish (V3).
    public async Task RefreshAsync(string deckId)
    {
        await using var scope = _scopes.CreateAsyncScope();
        await PublishAsync(scope.ServiceProvider, deckId);
    }

    // The passed state is only a signal for the deck: SW-4 notifies outside its per-deck lock, so two changes can arrive
    // here out of order. The position published is whatever the registry holds when this one's turn at the gate comes.
    public async Task OnChangedAsync(PresentationState state)
    {
        await using var scope = _scopes.CreateAsyncScope();
        await PublishAsync(scope.ServiceProvider, state.DeckId);
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
    private async Task PublishAsync(IServiceProvider services, string deckId)
    {
        var (feed, found) = await EnterAsync(services, deckId);
        if (feed is null)
        {
            return;
        }
        try
        {
            var state = await ComposeAsync(services, found!, NextSeq());
            await StampJoinSlideAsync(services, found!.SessionId!, state);
            await _audienceEvents.PublishAsync(state);
        }
        finally
        {
            feed.Gate.Release();
        }
    }

    // The one source of seq for every answer and push of every deck: strictly increasing, floored to the clock so it also rises
    // across a restart. One source (not one per deck entry, which comes and goes with the session) means a deck's seq keeps
    // rising across lobby, live, ended and lobby again, even under a clock that does not move.
    private long NextSeq()
    {
        var floor = _clock.GetUtcNow().ToUnixTimeMilliseconds();
        long current;
        long next;
        do
        {
            current = Interlocked.Read(ref _lastSeq);
            next = Math.Max(current + 1, floor);
        }
        while (Interlocked.CompareExchange(ref _lastSeq, next, current) != current);
        return next;
    }

    // Takes the gate of a deck whose session is open, with the session read again inside it, and returns the feed holding
    // the gate. For any other deck it returns no feed and holds nothing: an entry is only ever made for a live deck, and one
    // that finds its deck no longer live is removed again.
    private async Task<(DeckFeed? Feed, AudienceSessionRef? Found)> EnterAsync(IServiceProvider services, string deckId)
    {
        var sessions = services.GetRequiredService<IAudienceSessions>();
        var found = await sessions.ForDeckAsync(deckId);
        if (found is not { Phase: AudiencePhase.Live })
        {
            return (null, found);
        }
        while (true)
        {
            var feed = _feeds.GetOrAdd(deckId, _ => new DeckFeed());
            // Known cost for wave 2's load check: SW-4 awaits observers inline, and this gate is held across the reads below.
            await feed.Gate.WaitAsync();
            if (feed.Closed)
            {
                // Ended while this waited: the entry is gone, so ask again.
                feed.Gate.Release();
                continue;
            }
            found = await sessions.ForDeckAsync(deckId);
            if (found is { Phase: AudiencePhase.Live })
            {
                return (feed, found);
            }
            Close(deckId, feed);
            feed.Gate.Release();
            return (null, found);
        }
    }

    // Under the feed's gate.
    private void Close(string deckId, DeckFeed feed)
    {
        feed.Closed = true;
        _feeds.TryRemove(new KeyValuePair<string, DeckFeed>(deckId, feed));
    }

    private static async Task<AudienceState> ComposeAsync(IServiceProvider services, AudienceSessionRef found, long seq)
    {
        if (found.Phase != AudiencePhase.Live)
        {
            return new AudienceState(found.DeckId, seq, found.Phase, null, 0, null, null);
        }
        var presentation = await services.GetRequiredService<IPresentations>().GetAsync(found.DeckId);
        var db = services.GetRequiredService<AppDbContext>();
        var revision = await db.Decks.AsNoTracking().Where(d => d.Id == found.DeckId).Select(d => d.Revision).SingleAsync();
        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == found.DeckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
        // TODO: the room comes from IAudienceRoom.ForDeckAsync; null until the feed reads it.
        return new AudienceState(found.DeckId, seq, AudiencePhase.Live, Project(slides, presentation), revision, await ActivityAsync(services, found.DeckId), null);
    }

    // The activity on screen, in the scope the caller opened (the seam is per request). It is called under the deck's feed
    // gate, so it must only read: IAudienceActivity never calls IPollPublisher, which holds a gate of its own while it
    // publishes. Neither gate is ever taken while the other is held, so the two can't deadlock.
    private static async Task<JsonElement?> ActivityAsync(IServiceProvider services, string deckId)
    {
        var activity = await services.GetRequiredService<IAudienceActivity>().ForDeckAsync(deckId);
        return activity is null ? null : JsonSerializer.SerializeToElement(activity, JsonSerializerOptions.Web);
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

    // One deck's ordering: publishes go one at a time, so seq never goes backwards on the wire.
    private sealed class DeckFeed
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool Closed { get; set; }
    }
}
