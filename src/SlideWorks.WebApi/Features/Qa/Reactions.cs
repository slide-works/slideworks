using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Qa;

// The real IReactions (Q7, D6, D19). A tap is checked in this order, and a refused one changes nothing: no live session for
// the code, or a guest not on its roster, is NoSession; an emoji that isn't one of the five is Invalid; reactions off in the
// room (IAudienceRoom: the deck's setting and the switch of the slide on screen) is Refused reactions_off; a guest past 10
// taps in 5 seconds (ReactionLimiter) is Limited. A counted tap goes to ReactionFlusher under the slide on screen, which
// writes the totals, and into the deck's burst window here.
//
// Bursts: the first tap after a quiet BurstInterval is published at once as a reactions event (ReactionsBurst) to the watch
// group; taps inside the BurstInterval after a publish wait for it to pass and go out together then, so a deck gets at most
// four bursts a second and the last tap before a quiet period always goes out. One burst per slide tapped in the window.
// A deck's window, and its timer, closes after a BurstInterval with nothing to publish, so an idle deck holds no timer. The
// relay only routes; this class is the only one that makes the event.
//
// A singleton: each tap opens its own scope for the per-request seams. TimeProvider drives the window, so tests step it.
[RegisterSingleton(For = typeof(IReactions))]
public sealed class Reactions(
    IServiceScopeFactory scopes,
    IAudienceEvents events,
    ReactionLimiter limiter,
    ReactionFlusher flusher,
    TimeProvider time,
    ILogger<Reactions> logger) : IReactions, IDisposable
{
    public static readonly TimeSpan BurstInterval = TimeSpan.FromMilliseconds(250);

    // The five wire names (ReactionEmoji's JsonStringEnumMemberName), matched exactly.
    private static readonly FrozenDictionary<string, ReactionEmoji> _emojiByName = typeof(ReactionEmoji)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToFrozenDictionary(
            f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? throw new InvalidOperationException($"ReactionEmoji.{f.Name} has no wire name."),
            f => (ReactionEmoji)f.GetValue(null)!,
            StringComparer.Ordinal);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, BurstWindow> _windows = new(StringComparer.Ordinal);
    private bool _stopped;

    public async Task<ReactionResult> TapAsync(string code, string guestId, string? emoji)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (await services.GetRequiredService<GuestPollViews>().SeatAsync(code, guestId) is not { SessionId: { } sessionId } seat)
        {
            return new ReactionResult.NoSession();
        }
        if (emoji is null || !_emojiByName.TryGetValue(emoji, out var tapped))
        {
            return new ReactionResult.Invalid(GuestQaErrors.UNKNOWN_EMOJI);
        }
        // Null when the session ended since the seat was read.
        if (await services.GetRequiredService<IAudienceRoom>().ForDeckAsync(seat.DeckId) is not { } room)
        {
            return new ReactionResult.NoSession();
        }
        if (!room.Reactions)
        {
            return new ReactionResult.Refused(GuestQaErrors.REACTIONS_OFF);
        }
        if (!limiter.TryAcquire(guestId))
        {
            return new ReactionResult.Limited();
        }
        // With no presentation, or no slide left in the deck, nothing is on screen to count the tap against.
        if (await SlideOnScreenAsync(services, seat.DeckId) is { } shown)
        {
            flusher.Add(sessionId, shown.Id, tapped, guestId);
            Count(seat.DeckId, shown.Number, tapped);
        }
        return new ReactionResult.Counted();
    }

    public Task<ReactionCounts> TotalsAsync(Guid sessionId) => flusher.TotalsAsync(sessionId);

    public Task EndSessionAsync(Guid sessionId) => flusher.EndSessionAsync(sessionId);

    public void Dispose()
    {
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

    // The slide the guests see: the presenter's, clamped to the deck as IAudienceRoom and the feed clamp it.
    private static async Task<(int Number, Guid Id)?> SlideOnScreenAsync(IServiceProvider services, string deckId)
    {
        if (await services.GetRequiredService<IPresentations>().GetAsync(deckId) is not { } presentation)
        {
            return null;
        }
        var db = services.GetRequiredService<AppDbContext>();
        var count = await db.Slides.AsNoTracking().CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        if (count == 0)
        {
            return null;
        }
        var shown = Math.Clamp(presentation.Slide, 1, count);
        var slideId = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == shown - 1)
            .Select(s => s.Id)
            .SingleAsync();
        return (shown, slideId);
    }

    // Adds the tap to the deck's window. A deck with an open window is published when its interval passes; a deck with none
    // is published at once, on the thread pool rather than inside the guest's request.
    private void Count(string deckId, int slide, ReactionEmoji emoji)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }
            if (_windows.TryGetValue(deckId, out var window))
            {
                window.Add(slide, emoji);
                return;
            }
            window = new BurstWindow();
            window.Add(slide, emoji);
            _windows[deckId] = window;
        }
        _ = Task.Run(() => PumpAsync(deckId));
    }

    // The window's interval has passed: publish what was tapped in it, or close the window when nothing was.
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
        _ = PumpAsync(deckId);
    }

    private async Task PumpAsync(string deckId)
    {
        while (true)
        {
            IReadOnlyList<(int Slide, ReactionCounts Counts)> bursts;
            lock (_gate)
            {
                if (_stopped || !_windows.TryGetValue(deckId, out var window))
                {
                    return;
                }
                bursts = window.Take();
                if (bursts.Count == 0)
                {
                    _windows.Remove(deckId);
                    return;
                }
                window.PublishedAt = time.GetTimestamp();
            }
            foreach (var (slide, counts) in bursts)
            {
                await PublishAsync(new ReactionsBurst(deckId, slide, counts));
            }
            lock (_gate)
            {
                if (_stopped || !_windows.TryGetValue(deckId, out var window))
                {
                    return;
                }
                var wait = BurstInterval - time.GetElapsedTime(window.PublishedAt);
                if (wait > TimeSpan.Zero)
                {
                    window.Timer = time.CreateTimer(state => OnTick((string)state!), deckId, wait, Timeout.InfiniteTimeSpan);
                    return;
                }
            }
        }
    }

    // A publish that throws is logged and the window carries on: the presenter's counter misses one burst, and the totals
    // are not affected.
    private async Task PublishAsync(ReactionsBurst burst)
    {
        try
        {
            await events.PublishAsync(burst);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Publishing reactions on slide {Slide} of deck {DeckId} failed", burst.Slide, burst.DeckId);
        }
    }

    // A deck's burst window: the taps since its last publish per slide, when that publish started, and the timer that ends
    // the window.
    private sealed class BurstWindow
    {
        private readonly SortedDictionary<int, int[]> _taps = [];

        public long PublishedAt { get; set; }

        public ITimer? Timer { get; set; }

        public void Add(int slide, ReactionEmoji emoji)
        {
            if (!_taps.TryGetValue(slide, out var counts))
            {
                counts = new int[Enum.GetValues<ReactionEmoji>().Length];
                _taps[slide] = counts;
            }
            counts[(int)emoji]++;
        }

        // Each slide's counts, in slide order, and none left.
        public IReadOnlyList<(int Slide, ReactionCounts Counts)> Take()
        {
            (int, ReactionCounts)[] bursts = [.. _taps.Select(t => (t.Key, ReactionFlusher.CountsOf(t.Value)))];
            _taps.Clear();
            return bursts;
        }
    }
}
