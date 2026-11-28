using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.WebApi.Realtime.Presenting;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SlideWorks.WebApi.Realtime;

// The state behind IAudienceRelay and the presentation observer: one instance, so both share one seq and one ordering.
// Carries the presenter's position to the guests (J4). It follows SW-4's presentation state (IPresentationObserver) and
// the deck's own changes (IDeckEvents), and publishes an audienceState for every one that lands on a deck with an open
// session. The phase always comes from IAudienceSessions and the one slide on screen is projected with no notes and no
// deep-dive mark (D15); Current, Activity and Room are filled only while the phase is live. Activity is what IAudienceActivity
// says is on screen (a poll, V3), and Room what IAudienceRoom says the room may do (SW-9 Q1), each serialised as it goes
// to the guest.
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
//
// Languages (SW-883 K5, D11): a publish composes one state per effective language in use, plus the primary, all at one seq,
// each with the slots TranslationMerge gives for that language and routed by its Group to that language's slide-state group.
// Before it composes, it places every connection on the deck's roster in the group of its effective language again, so a
// settings change, a primary change or turning translations off moves connections with the next publish. With translations
// off that is one composition and one message, as before. The cache holds one state per language, with the settings it
// was composed under.
[RegisterSingleton]
public sealed class AudienceFeed : IDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IAudienceEvents _audienceEvents;
    private readonly IHubContext<DeckHub> _hub;
    private readonly TimeProvider _clock;
    private readonly QaRevisions _qaRevisions;
    private readonly IDisposable _deckSubscription;
    private long _lastSeq;
    private readonly ConcurrentDictionary<string, DeckFeed> _feeds = new(StringComparer.Ordinal);

    public AudienceFeed(IServiceScopeFactory scopes, IAudienceEvents audienceEvents, IDeckEvents deckEvents, TimeProvider clock, QaRevisions qaRevisions, IHubContext<DeckHub> hub)
    {
        _hub = hub;
        _qaRevisions = qaRevisions;
        _scopes = scopes;
        _audienceEvents = audienceEvents;
        _clock = clock;
        _deckSubscription = deckEvents.Subscribe(null, OnDeckEventAsync);
    }

    public void Dispose() => _deckSubscription.Dispose();

    // The state in the language the guest picked when the deck offers it, else in the primary (K5). Lobby and ended states
    // carry no language: there is no slide to translate.
    public async Task<AudienceState?> CurrentAsync(string deckId, string? language)
    {
        await using var scope = _scopes.CreateAsyncScope();
        // Taken before the phase is read, so an answer built from an older read always carries an older seq than any push
        // that read could have missed (a lobby read that began before Go live, answered after the first live push).
        var early = NextSeq();
        var (feed, found) = await EnterAsync(scope.ServiceProvider, deckId, cachedLanguage: language, allowCached: true);
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
            // SW-286: the gate is held only to copy the last composed state under a new seq; a miss composes (and caches) it.
            if (CachedFor(feed, language) is { } cached)
            {
                // The cached state's Group is its own language's, which `with` copies.
                return cached with { Seq = NextSeq() };
            }
            var seq = NextSeq();
            var live = await LiveAsync(scope.ServiceProvider, found!);
            var settings = live.Snapshot.Settings;
            var effective = AudienceSlideGroups.Effective(settings, language);
            var composed = await ComposeLanguageAsync(scope.ServiceProvider, found!.DeckId, live, effective, seq);
            if (feed.Settings is not { } held || !SameSettings(held, settings))
            {
                // The settings moved since the last publish: the other languages' states were composed under the old ones.
                feed.ClearCache();
                feed.Settings = settings;
            }
            feed.Cache(effective, composed);
            return composed;
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
            feed.ClearCache();
            feed.Settings = null;
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
        // Dropped first, so a compose that throws leaves nothing stale behind for the next join.
        feed.ClearCache();
        feed.Settings = null;
        try
        {
            var seq = NextSeq();
            var live = await LiveAsync(services, found!);
            var settings = live.Snapshot.Settings;
            // Resolve each connection's language and move its group first, then compose (K5's server order).
            var languages = await RegroupAsync(services, deckId, settings);
            var states = new List<AudienceState>(languages.Count);
            foreach (var language in languages)
            {
                states.Add(await ComposeLanguageAsync(services, deckId, live, language, seq));
            }
            feed.Settings = settings;
            foreach (var (language, state) in languages.Zip(states))
            {
                feed.Cache(language, state);
            }
            // The slide on screen is the same in every language; the primary's state (first) stands for them all.
            await StampJoinSlideAsync(services, found!.SessionId!, states[0]);
            foreach (var state in states)
            {
                await _audienceEvents.PublishAsync(state);
            }
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
    // With allowCached, a feed that holds a composed state is returned without the second read of the session: the closed
    // flag, set under the gate when the session ends, says the entry is still the open session's.
    private async Task<(DeckFeed? Feed, AudienceSessionRef? Found)> EnterAsync(IServiceProvider services, string deckId, string? cachedLanguage = null, bool allowCached = false)
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
            if (allowCached && CachedFor(feed, cachedLanguage) is not null)
            {
                return (feed, found);
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

    // The last composed live state, when it can still stand for a new answer. Under the feed's gate. Every publish replaces it
    // (a move, a deck change, Go live, and the refresh a poll or Q&A settings change makes), and the end drops it with the
    // entry. A new question moves only the room's list revision, which lives in memory, so that is compared here instead.
    // The language is the guest's pick; the state cached is its effective language's under the settings it was composed with.
    private AudienceState? CachedFor(DeckFeed feed, string? language)
    {
        if (feed.Settings is not { } settings || feed.Cached(AudienceSlideGroups.Effective(settings, language)) is not { } cached)
        {
            return null;
        }
        if (cached.Room is { } room && room.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.String
            && room.TryGetProperty("listRevision", out var revision) && revision.GetInt64() != _qaRevisions.Current(round.GetString()!))
        {
            return null;
        }
        return cached;
    }

    // Under the feed's gate.
    private void Close(string deckId, DeckFeed feed)
    {
        feed.Closed = true;
        _feeds.TryRemove(new KeyValuePair<string, DeckFeed>(deckId, feed));
    }

    // A lobby or an ended state: no slide, no language (only a live state is composed per language).
    private static Task<AudienceState> ComposeAsync(IServiceProvider services, AudienceSessionRef found, long seq)
        => found.Phase == AudiencePhase.Live
            ? ComposePrimaryAsync(services, found, seq)
            : Task.FromResult(new AudienceState(found.DeckId, seq, found.Phase, null, 0, null, null, null));

    // A live deck answered outside its feed entry (it went live between the two reads of the phase): in the primary language.
    private static async Task<AudienceState> ComposePrimaryAsync(IServiceProvider services, AudienceSessionRef found, long seq)
    {
        var live = await LiveAsync(services, found);
        return await ComposeLanguageAsync(services, found.DeckId, live, live.Snapshot.Settings.Primary, seq);
    }

    // What every language's state of one compose shares: the deck loaded once through TranslationMerge (never per language),
    // the presenter's position, the deck's revision and the room.
    private static async Task<LiveDeck> LiveAsync(IServiceProvider services, AudienceSessionRef found)
    {
        var presentation = await services.GetRequiredService<IPresentations>().GetAsync(found.DeckId);
        var db = services.GetRequiredService<AppDbContext>();
        var revision = await db.Decks.AsNoTracking().Where(d => d.Id == found.DeckId).Select(d => d.Revision).SingleAsync();
        var snapshot = await services.GetRequiredService<TranslationMerge>().LoadAsync(found.DeckId);
        return new LiveDeck(snapshot, presentation, revision, await RoomAsync(services, found.DeckId));
    }

    // One effective language's live state, routed to its slide-state group. Language and the activity's language are set only
    // while translations are on, so a deck with them off composes exactly as before.
    private static async Task<AudienceState> ComposeLanguageAsync(IServiceProvider services, string deckId, LiveDeck live, string language, long seq)
    {
        var settings = live.Snapshot.Settings;
        var (current, fallbackLines) = Project(live.Snapshot, live.Presentation, language);
        var state = new AudienceState(
            deckId,
            seq,
            AudiencePhase.Live,
            current,
            live.Revision,
            // Null for the primary (and with translations off), so the activity skips its translation reads; once per language.
            await ActivityAsync(services, deckId, settings.Enabled && language != settings.Primary ? language : null),
            live.Room,
            // D24: FallbackLines already leaves out emphasis entries; taken as it is.
            settings.Enabled ? new AudienceLanguage(language, fallbackLines) : null);
        return state with { Group = AudienceSlideGroups.Name(deckId, language) };
    }

    // Places every connection on the deck's roster in the slide-state group of its effective language under these settings,
    // and returns the languages to compose: the primary first, then each other effective language in use.
    private async Task<IReadOnlyList<string>> RegroupAsync(IServiceProvider services, string deckId, TranslationSettings settings)
    {
        var roster = services.GetRequiredService<IAudienceRoster>();
        var languages = new List<string> { settings.Primary };
        foreach (var seat in await roster.SeatsAsync(deckId))
        {
            var effective = AudienceSlideGroups.Effective(settings, seat.Chosen);
            if (!languages.Contains(effective, StringComparer.Ordinal))
            {
                languages.Add(effective);
            }
            if (seat.Placed != effective)
            {
                await AudienceSlideGroups.PlaceAsync(roster, _hub.Groups, deckId, seat.ConnectionId, effective);
            }
        }
        return languages;
    }

    private static bool SameSettings(TranslationSettings held, TranslationSettings read)
        => held.Enabled == read.Enabled && held.Primary == read.Primary && held.Languages.SequenceEqual(read.Languages, StringComparer.Ordinal);

    // What the room may do on the slide on screen (SW-9 Q1, Q3), in the scope the caller opened (the seam is per request).
    // Read only while live: the phase above decides, so every other phase carries a null room whatever the seam would say.
    // Like the activity it is read under the deck's feed gate, so it must only read; IAudienceRoom never publishes.
    private static async Task<JsonElement?> RoomAsync(IServiceProvider services, string deckId)
    {
        var room = await services.GetRequiredService<IAudienceRoom>().ForDeckAsync(deckId);
        return room is null ? null : JsonSerializer.SerializeToElement(room, JsonSerializerOptions.Web);
    }

    // The activity on screen, in the scope the caller opened (the seam is per request). It is called under the deck's feed
    // gate, so it must only read: IAudienceActivity never calls IPollPublisher, which holds a gate of its own while it
    // publishes. Neither gate is ever taken while the other is held, so the two can't deadlock.
    private static async Task<JsonElement?> ActivityAsync(IServiceProvider services, string deckId, string? language)
    {
        var activity = await services.GetRequiredService<IAudienceActivity>().ForDeckAsync(deckId, language);
        return activity is null ? null : JsonSerializer.SerializeToElement(activity, JsonSerializerOptions.Web);
    }

    // The one slide on screen, its slots merged in the language, and the merge's fallback lines (0 with no slide on screen).
    // A position past the end of the deck (slides removed while live) shows the last slide.
    private static (AudienceSlide? Current, int FallbackLines) Project(DeckSnapshot snapshot, PresentationState? presentation, string language)
    {
        var slides = snapshot.Slides;
        if (presentation is null || slides.Count == 0)
        {
            return (null, 0);
        }
        var index = Math.Clamp(presentation.Slide, 1, slides.Count);
        var slide = slides[index - 1];
        var step = Math.Clamp(presentation.Step, 0, slide.Steps);
        // The surface is the slide's stored one, else the layout's own. TODO: a custom layout (wave 2) is not in the built-in
        // catalogue, so it reads "light" unless the slide stores a surface. No template lookup here: the feed is a singleton.
        var surface = slide.Surface ?? (LayoutCatalogue.Find(slide.Layout)?.Dark == true ? SlideSurface.DARK : SlideSurface.LIGHT);
        var merged = TranslationMerge.For(slide, snapshot.Template, snapshot.Settings, language);
        return (new AudienceSlide(index, slides.Count, slide.Layout, merged.Slots, slide.Steps, step, presentation.Black, surface), merged.FallbackLines);
    }

    // J8: the first time the live state lands on a join slide. A conditional update, so a repeat (or a second process)
    // leaves the first stamp alone.
    private async Task StampJoinSlideAsync(IServiceProvider services, string sessionId, AudienceState state)
    {
        if (state.Current is not { Layout: JoinLayout.NAME })
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

        // SW-286: the last live state composed for this deck in each effective language, so a join does not read the database
        // under the gate. Settings are the translation settings they were composed under; null when nothing is cached.
        private readonly Dictionary<string, AudienceState> _cache = new(StringComparer.Ordinal);

        public TranslationSettings? Settings { get; set; }

        public AudienceState? Cached(string language) => _cache.GetValueOrDefault(language);

        public void Cache(string language, AudienceState state) => _cache[language] = state;

        public void ClearCache() => _cache.Clear();
    }

    private sealed record LiveDeck(DeckSnapshot Snapshot, PresentationState? Presentation, long Revision, JsonElement? Room);
}
