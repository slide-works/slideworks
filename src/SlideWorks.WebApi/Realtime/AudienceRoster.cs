using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Live;

namespace SlideWorks.WebApi.Realtime;

// The roster of every deck's audience, in memory and per process (J4, D22). It tracks each audience connection (deck, guest
// id, connection id) in every phase; a guest counts once however many connections it has, and leaves when its last
// connection goes. While a deck has an open session it also writes the session's data (J8): an AudienceJoin row the first
// time each guest is counted in a session, and PeakJoined, the most guests counted at once.
//
// The roster is a singleton and AppDbContext is per request, so each write opens its own scope, as CachingCardRenderer does.
// It never reads the caller (ICurrentActor, IHttpContextAccessor): under long polling a hub call runs after its request has
// finished, and a guard test checks it.
[RegisterSingleton(For = typeof(IAudienceRoster))]
public sealed class AudienceRoster(IServiceScopeFactory scopes, IAudienceEvents events, TimeProvider clock, ILogger<AudienceRoster> log) : IAudienceRoster
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeckAudience> _decks = new(StringComparer.Ordinal);

    // Guests already written for a session this process, so a reconnect costs no query. After a restart the table decides.
    private readonly HashSet<(string SessionId, string GuestId)> _recorded = [];

    // How long a deck's entry outlives its last connection and session, so it can still remember the session it closed.
    private static readonly TimeSpan _closedMemory = TimeSpan.FromMinutes(5);

    public async Task<int> JoinAsync(string deckId, string? sessionId, string guestId, string connectionId)
    {
        Counted counted;
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var existing))
            {
                RemoveLocked(connectionId, existing);
            }
            _connections[connectionId] = new Connection(deckId, guestId);
            var audience = AudienceOf(deckId);
            audience.Add(guestId, connectionId);
            // Only OpenedAsync and ClosedAsync decide the deck's session. A join may seed it when the roster has none (the first
            // join after a restart), but a null from the hub never clears it, a session id that differs from the open one is
            // ignored, and a session ClosedAsync closed never comes back: the join carries what the hub read before it ran.
            if (audience.SessionId == null && sessionId != null && sessionId != audience.LastClosedSessionId)
            {
                audience.Open(sessionId);
            }
            counted = CountedLocked(deckId, audience);
        }
        await WriteAsync(counted);
        return counted.Count;
    }

    public async Task<RosterLeave?> LeaveAsync(string connectionId)
    {
        Connection? connection;
        string? sessionId;
        int remaining;
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out connection))
            {
                return null;
            }
            sessionId = _decks.GetValueOrDefault(connection.DeckId)?.SessionId;
            RemoveLocked(connectionId, connection);
            remaining = _decks.GetValueOrDefault(connection.DeckId)?.GuestCount ?? 0;
        }
        if (sessionId != null && remaining == 0)
        {
            await ForgetIfSessionGoneAsync(connection.DeckId, sessionId);
        }
        // A lobby or ended connection leaves quietly: there is no count for the owner to hear.
        return sessionId == null ? null : new RosterLeave(connection.DeckId, sessionId, remaining);
    }

    public Task<int> CountAsync(string sessionId)
    {
        lock (_gate)
        {
            return Task.FromResult(_decks.Values.Where(d => d.SessionId == sessionId).Select(d => d.GuestCount).FirstOrDefault());
        }
    }

    public async Task OpenedAsync(string deckId, string sessionId)
    {
        Counted counted;
        lock (_gate)
        {
            var audience = AudienceOf(deckId);
            if (audience.SessionId != null && audience.SessionId != sessionId)
            {
                _recorded.RemoveWhere(r => r.SessionId == audience.SessionId);
            }
            audience.Open(sessionId);
            counted = CountedLocked(deckId, audience);
        }
        try
        {
            await WriteAsync(counted);
        }
        catch (Exception ex)
        {
            // The roster is bookkeeping and never gates Go live: the rows are retried as guests join, and a retried Go live
            // answers with the open session.
            log.LogError(ex, "Could not record the guests already waiting for session {SessionId}", sessionId);
        }
        await events.PublishAsync(new JoinedCount(deckId, counted.Count));
    }

    public Task ClosedAsync(string deckId, string sessionId)
    {
        lock (_gate)
        {
            // Recorded even when the deck has no entry (after a restart, before any guest rejoined), so a join already in
            // flight with this session's id can't reopen it. The entry then lives for the closed memory.
            var audience = AudienceOf(deckId);
            _recorded.RemoveWhere(r => r.SessionId == audience.SessionId || r.SessionId == sessionId);
            audience.Close(clock.GetUtcNow(), sessionId);
            DropIfIdleLocked(deckId, audience);
            foreach (var (id, other) in _decks.ToList())
            {
                DropIfIdleLocked(id, other);
            }
        }
        return Task.CompletedTask;
    }

    private DeckAudience AudienceOf(string deckId)
    {
        if (!_decks.TryGetValue(deckId, out var audience))
        {
            audience = new DeckAudience();
            _decks[deckId] = audience;
        }
        return audience;
    }

    private void RemoveLocked(string connectionId, Connection connection)
    {
        _connections.Remove(connectionId);
        if (!_decks.TryGetValue(connection.DeckId, out var audience))
        {
            return;
        }
        audience.Remove(connection.GuestId, connectionId);
        DropIfIdleLocked(connection.DeckId, audience);
    }

    // A deck's entry goes when it has no connections and no open session, once it no longer needs to remember a closed one.
    private void DropIfIdleLocked(string deckId, DeckAudience audience)
    {
        if (audience.GuestCount == 0 && audience.SessionId == null && !audience.RemembersClosed(clock.GetUtcNow(), _closedMemory))
        {
            _decks.Remove(deckId);
        }
    }

    // The last guest left a deck whose session the roster still holds open. If the session row is gone (the deck was deleted
    // while live, so ClosedAsync never ran) or has ended, the entry would stay for the life of the process.
    private async Task ForgetIfSessionGoneAsync(string deckId, string sessionId)
    {
        try
        {
            var open = Guid.TryParse(sessionId, out var id);
            if (open)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                open = await db.AudienceSessions.AnyAsync(s => s.Id == id && s.EndedAt == null);
            }
            if (open)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not check whether session {SessionId} is still open", sessionId);
            return;
        }
        lock (_gate)
        {
            if (_decks.TryGetValue(deckId, out var audience) && audience.GuestCount == 0 && audience.SessionId == sessionId)
            {
                _recorded.RemoveWhere(r => r.SessionId == sessionId);
                audience.Close(clock.GetUtcNow(), sessionId);
                DropIfIdleLocked(deckId, audience);
            }
        }
    }

    // The guests of the deck's open session that have no row yet, marked as written so a second connection doesn't repeat it.
    // The peak is written only when the count passes the last one written for the session.
    private Counted CountedLocked(string deckId, DeckAudience audience)
    {
        var count = audience.GuestCount;
        if (audience.SessionId is not { } sessionId)
        {
            return new Counted(deckId, null, count, false, []);
        }
        var fresh = audience.GuestIds.Where(guest => _recorded.Add((sessionId, guest))).ToList();
        var writePeak = count > audience.PeakWritten;
        if (writePeak)
        {
            audience.PeakWritten = count;
        }
        return new Counted(deckId, sessionId, count, writePeak, fresh);
    }

    // SW-286: group commit. A room of guests joining at once would each open a scope and write their own row and peak against a
    // database that takes one writer at a time, so the writes queue on the connection pool and slow everything else (a presenter's
    // move reads the same database). Writes wait for one turn at a time; whoever has it writes every write queued by then in one
    // pass, so 200 guests joining together cost a handful of transactions. Each caller still returns only once its own rows landed
    // (or its write failed), as before.
    private async Task WriteAsync(Counted counted)
    {
        // A second tab, or a count at or below the peak, has nothing to write and costs no query.
        if (!Guid.TryParse(counted.SessionId, out _) || (counted.NewGuests.Count == 0 && !counted.WritePeak))
        {
            return;
        }
        var write = new PendingWrite(counted);
        lock (_pending)
        {
            _pending.Add(write);
        }
        await _writeTurn.WaitAsync();
        try
        {
            if (!write.Done)
            {
                List<PendingWrite> batch;
                lock (_pending)
                {
                    batch = [.. _pending];
                    _pending.Clear();
                }
                await WriteBatchAsync(batch);
            }
        }
        finally
        {
            _writeTurn.Release();
        }
        if (write.Error is { } error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private async Task WriteBatchAsync(List<PendingWrite> batch)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var session in batch.GroupBy(w => Guid.Parse(w.Counted.SessionId!)))
            {
                var sessionId = session.Key;
                // A session deleted with its deck while guests were still connected has no row to write under.
                if (!await db.AudienceSessions.AnyAsync(s => s.Id == sessionId))
                {
                    continue;
                }
                var guests = session.SelectMany(w => w.Counted.NewGuests).Distinct().ToList();
                if (guests.Count > 0)
                {
                    var known = await db.AudienceJoins.Where(j => j.SessionId == sessionId && guests.Contains(j.GuestId)).Select(j => j.GuestId).ToListAsync();
                    // A guest counted when its session opened was waiting in the lobby, so JoinedAt is the time Go live ran, not the
                    // time the guest joined. Measures of join time (E1c) must clamp or exclude it: it can be at or before the join
                    // slide's first showing.
                    var now = clock.GetUtcNow();
                    db.AudienceJoins.AddRange(guests.Except(known).Select(guest => new AudienceJoin { SessionId = sessionId, GuestId = guest, JoinedAt = now }));
                    await db.SaveChangesAsync();
                }
                if (session.Where(w => w.Counted.WritePeak).Select(w => w.Counted.Count).DefaultIfEmpty(0).Max() is var peak and > 0)
                {
                    await db.AudienceSessions.Where(s => s.Id == sessionId && s.PeakJoined < peak).ExecuteUpdateAsync(s => s.SetProperty(x => x.PeakJoined, peak));
                }
            }
        }
        catch (Exception ex)
        {
            // The rows did not land: forget that they were written so the next join retries them.
            lock (_gate)
            {
                foreach (var counted in batch.Select(w => w.Counted))
                {
                    foreach (var guest in counted.NewGuests)
                    {
                        _recorded.Remove((counted.SessionId!, guest));
                    }
                    if (_decks.TryGetValue(counted.DeckId, out var audience) && audience.SessionId == counted.SessionId)
                    {
                        audience.PeakWritten = 0;
                    }
                }
            }
            foreach (var write in batch)
            {
                write.Error = ex;
            }
        }
        finally
        {
            foreach (var write in batch)
            {
                write.Done = true;
            }
        }
    }

    private readonly SemaphoreSlim _writeTurn = new(1, 1);
    private readonly List<PendingWrite> _pending = [];

    private sealed class PendingWrite(Counted counted)
    {
        public Counted Counted { get; } = counted;

        public volatile bool Done;

        public Exception? Error;
    }

    private sealed record Connection(string DeckId, string GuestId);

    private sealed record Counted(string DeckId, string? SessionId, int Count, bool WritePeak, List<string> NewGuests);

    private sealed class DeckAudience
    {
        private readonly Dictionary<string, HashSet<string>> _guests = new(StringComparer.Ordinal);

        public string? SessionId { get; private set; }

        // The session ClosedAsync last closed, so a late join that carries it can't bring it back.
        public string? LastClosedSessionId { get; private set; }

        private DateTimeOffset _closedAt;

        // The highest count written to the open session's PeakJoined this process.
        public int PeakWritten { get; set; }

        public void Open(string sessionId)
        {
            SessionId = sessionId;
            PeakWritten = 0;
        }

        public void Close(DateTimeOffset now, string sessionId)
        {
            LastClosedSessionId = sessionId;
            SessionId = null;
            _closedAt = now;
        }

        public bool RemembersClosed(DateTimeOffset now, TimeSpan memory) => LastClosedSessionId != null && now - _closedAt < memory;

        public int GuestCount => _guests.Count;

        public IEnumerable<string> GuestIds => _guests.Keys;

        public void Add(string guestId, string connectionId)
        {
            if (!_guests.TryGetValue(guestId, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                _guests[guestId] = connections;
            }
            connections.Add(connectionId);
        }

        // A guest leaves when its last connection goes.
        public void Remove(string guestId, string connectionId)
        {
            if (_guests.TryGetValue(guestId, out var connections) && connections.Remove(connectionId) && connections.Count == 0)
            {
                _guests.Remove(guestId);
            }
        }
    }
}
