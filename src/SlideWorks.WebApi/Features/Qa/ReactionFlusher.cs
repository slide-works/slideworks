using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Qa.Data;

namespace SlideWorks.WebApi.Features.Qa;

// The reaction taps not yet written, per session (Q7, D19), and their writes: totals per session, slide and emoji added to
// ReactionTotal, and a ReactionGuest row for each guest who tapped, where the session has none for it yet. Never a row per
// tap. A session's counts are written Interval after its first unwritten tap, and by EndSessionAsync; each write is one
// SaveChanges, so one transaction. A session with nothing unwritten holds no timer and no entry.
//
// Writes, and TotalsAsync's read of the stored totals plus what is in memory, take one gate, so a total never misses or
// counts twice the taps a write has taken out of memory but not yet committed. A timed write that fails is logged and its
// counts go back in memory for the next one; the write at the end of a session that fails is logged and dropped, so a
// presenter can always end. A restart loses what is in memory, up to Interval of taps (D19). Only Reactions calls it.
// Guest ids are written, never logged.
[RegisterSingleton]
public sealed class ReactionFlusher(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReactionFlusher> logger) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _writing = new(1, 1);
    private readonly Dictionary<Guid, Unwritten> _sessions = [];
    private bool _stopped;

    // A tap counted against a slide of a live session. The first unwritten tap of a session starts its timer.
    public void Add(Guid sessionId, Guid slideId, ReactionEmoji emoji, string guestId)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }
            UnwrittenOf(sessionId).Add(slideId, emoji, guestId);
        }
    }

    // The session's totals per emoji: what is stored plus what is still in memory.
    public async Task<ReactionCounts> TotalsAsync(Guid sessionId)
    {
        await _writing.WaitAsync();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.ReactionTotals.AsNoTracking()
                .Where(t => t.SessionId == sessionId)
                .GroupBy(t => t.Emoji)
                .Select(g => new { Emoji = g.Key, Count = g.Sum(t => t.Count) })
                .ToListAsync();
            var counts = new int[Enum.GetValues<ReactionEmoji>().Length];
            foreach (var total in stored)
            {
                counts[(int)total.Emoji] += total.Count;
            }
            lock (_gate)
            {
                if (_sessions.TryGetValue(sessionId, out var unwritten))
                {
                    unwritten.AddTo(counts);
                }
            }
            return CountsOf(counts);
        }
        finally
        {
            _writing.Release();
        }
    }

    // Writes what is still in memory for the session and forgets it. Idempotent; never throws for a failed write.
    public async Task EndSessionAsync(Guid sessionId)
    {
        await _writing.WaitAsync();
        try
        {
            if (Take(sessionId) is not { } unwritten)
            {
                return;
            }
            try
            {
                await WriteAsync(sessionId, unwritten);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Writing the reactions of session {SessionId} at its end failed; {Taps} taps are dropped", sessionId, unwritten.Taps);
            }
        }
        finally
        {
            _writing.Release();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _stopped = true;
            foreach (var unwritten in _sessions.Values)
            {
                unwritten.Timer?.Dispose();
            }
            _sessions.Clear();
        }
    }

    // A ReactionCounts from counts indexed by ReactionEmoji.
    public static ReactionCounts CountsOf(IReadOnlyList<int> counts)
        => new(
            counts[(int)ReactionEmoji.ThumbsUp],
            counts[(int)ReactionEmoji.Heart],
            counts[(int)ReactionEmoji.Laugh],
            counts[(int)ReactionEmoji.Wow],
            counts[(int)ReactionEmoji.Clap]);

    // Under _gate.
    private Unwritten UnwrittenOf(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var unwritten))
        {
            unwritten = new Unwritten
            {
                Timer = time.CreateTimer(state => _ = FlushAsync((Guid)state!), sessionId, Interval, Timeout.InfiniteTimeSpan),
            };
            _sessions[sessionId] = unwritten;
        }
        return unwritten;
    }

    // Takes the session's unwritten taps out of memory and stops its timer, or null when it has none.
    private Unwritten? Take(Guid sessionId)
    {
        lock (_gate)
        {
            if (!_sessions.Remove(sessionId, out var unwritten))
            {
                return null;
            }
            unwritten.Timer?.Dispose();
            unwritten.Timer = null;
            return unwritten;
        }
    }

    // A session's timer is due. Runs on the timer, so nothing it throws may escape: a failed write is logged and kept.
    private async Task FlushAsync(Guid sessionId)
    {
        await _writing.WaitAsync();
        try
        {
            if (Take(sessionId) is not { } unwritten)
            {
                return;
            }
            try
            {
                await WriteAsync(sessionId, unwritten);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Writing the reactions of session {SessionId} failed; {Taps} taps are kept for the next write", sessionId, unwritten.Taps);
                lock (_gate)
                {
                    if (!_stopped)
                    {
                        UnwrittenOf(sessionId).Merge(unwritten);
                    }
                }
            }
        }
        finally
        {
            _writing.Release();
        }
    }

    // One SaveChanges: the totals added to, and the guests not yet recorded for the session. Only this class writes these
    // rows, one write at a time, so reading them first and saving after can't race another writer. A session that is gone
    // (deleted with its deck) has nothing to write under.
    private async Task WriteAsync(Guid sessionId, Unwritten unwritten)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.AudienceSessions.AnyAsync(s => s.Id == sessionId))
        {
            logger.LogWarning("Session {SessionId} is gone; {Taps} unwritten reaction taps are dropped", sessionId, unwritten.Taps);
            return;
        }
        var totals = await db.ReactionTotals.Where(t => t.SessionId == sessionId).ToListAsync();
        foreach (var ((slideId, emoji), count) in unwritten.Counts)
        {
            var total = totals.SingleOrDefault(t => t.SlideId == slideId && t.Emoji == emoji);
            if (total is null)
            {
                db.ReactionTotals.Add(new ReactionTotal { SessionId = sessionId, SlideId = slideId, Emoji = emoji, Count = count });
            }
            else
            {
                total.Count += count;
            }
        }
        var guests = unwritten.Guests.ToList();
        var known = await db.ReactionGuests.Where(g => g.SessionId == sessionId && guests.Contains(g.GuestId)).Select(g => g.GuestId).ToListAsync();
        db.ReactionGuests.AddRange(guests.Except(known, StringComparer.Ordinal).Select(guest => new ReactionGuest { SessionId = sessionId, GuestId = guest }));
        await db.SaveChangesAsync();
    }

    // One session's taps not yet written: counts per slide and emoji, the guests who tapped, and the timer that writes them.
    private sealed class Unwritten
    {
        private readonly Dictionary<(Guid SlideId, ReactionEmoji Emoji), int> _counts = [];
        private readonly HashSet<string> _guests = new(StringComparer.Ordinal);

        public ITimer? Timer { get; set; }

        public IReadOnlyDictionary<(Guid SlideId, ReactionEmoji Emoji), int> Counts => _counts;

        public IReadOnlyCollection<string> Guests => _guests;

        public int Taps => _counts.Values.Sum();

        public void Add(Guid slideId, ReactionEmoji emoji, string guestId)
        {
            _counts[(slideId, emoji)] = _counts.GetValueOrDefault((slideId, emoji)) + 1;
            _guests.Add(guestId);
        }

        public void Merge(Unwritten other)
        {
            foreach (var (key, count) in other._counts)
            {
                _counts[key] = _counts.GetValueOrDefault(key) + count;
            }
            _guests.UnionWith(other._guests);
        }

        public void AddTo(int[] counts)
        {
            foreach (var ((_, emoji), count) in _counts)
            {
                counts[(int)emoji] += count;
            }
        }
    }
}
