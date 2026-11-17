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
public sealed class AudienceRoster(IServiceScopeFactory scopes, IAudienceEvents events, TimeProvider clock) : IAudienceRoster
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeckAudience> _decks = new(StringComparer.Ordinal);

    // Guests already written for a session this process, so a reconnect costs no query. After a restart the table decides.
    private readonly HashSet<(string SessionId, string GuestId)> _recorded = [];

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
            audience.SessionId = sessionId;
            counted = CountedLocked(audience);
        }
        await WriteAsync(counted);
        return counted.Count;
    }

    public Task<RosterLeave?> LeaveAsync(string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                return Task.FromResult<RosterLeave?>(null);
            }
            var sessionId = _decks.GetValueOrDefault(connection.DeckId)?.SessionId;
            RemoveLocked(connectionId, connection);
            var remaining = _decks.GetValueOrDefault(connection.DeckId)?.GuestCount ?? 0;
            // A lobby or ended connection leaves quietly: there is no count for the owner to hear.
            return Task.FromResult(sessionId == null ? null : new RosterLeave(connection.DeckId, sessionId, remaining));
        }
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
            audience.SessionId = sessionId;
            counted = CountedLocked(audience);
        }
        await WriteAsync(counted);
        await events.PublishAsync(new JoinedCount(deckId, counted.Count));
    }

    public Task ClosedAsync(string deckId)
    {
        lock (_gate)
        {
            if (_decks.TryGetValue(deckId, out var audience))
            {
                _recorded.RemoveWhere(r => r.SessionId == audience.SessionId);
                audience.SessionId = null;
                if (audience.GuestCount == 0)
                {
                    _decks.Remove(deckId);
                }
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
        if (audience.GuestCount == 0 && audience.SessionId == null)
        {
            _decks.Remove(connection.DeckId);
        }
    }

    // The guests of the deck's open session that have no row yet, marked as written so a second connection doesn't repeat it.
    private Counted CountedLocked(DeckAudience audience)
    {
        var count = audience.GuestCount;
        if (audience.SessionId is not { } sessionId)
        {
            return new Counted(null, count, []);
        }
        var fresh = audience.GuestIds.Where(guest => _recorded.Add((sessionId, guest))).ToList();
        return new Counted(sessionId, count, fresh);
    }

    private async Task WriteAsync(Counted counted)
    {
        if (!Guid.TryParse(counted.SessionId, out var sessionId))
        {
            return;
        }
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            // A session deleted with its deck while guests were still connected has no row to write under.
            if (!await db.AudienceSessions.AnyAsync(s => s.Id == sessionId))
            {
                return;
            }
            if (counted.NewGuests.Count > 0)
            {
                var known = await db.AudienceJoins.Where(j => j.SessionId == sessionId && counted.NewGuests.Contains(j.GuestId)).Select(j => j.GuestId).ToListAsync();
                var now = clock.GetUtcNow();
                db.AudienceJoins.AddRange(counted.NewGuests.Except(known).Select(guest => new AudienceJoin { SessionId = sessionId, GuestId = guest, JoinedAt = now }));
                await db.SaveChangesAsync();
            }
            await db.AudienceSessions.Where(s => s.Id == sessionId && s.PeakJoined < counted.Count).ExecuteUpdateAsync(s => s.SetProperty(x => x.PeakJoined, counted.Count));
        }
        catch
        {
            // The rows did not land: forget that they were written so the next join retries them.
            lock (_gate)
            {
                foreach (var guest in counted.NewGuests)
                {
                    _recorded.Remove((counted.SessionId, guest));
                }
            }
            throw;
        }
    }

    private sealed record Connection(string DeckId, string GuestId);

    private sealed record Counted(string? SessionId, int Count, List<string> NewGuests);

    private sealed class DeckAudience
    {
        private readonly Dictionary<string, HashSet<string>> _guests = new(StringComparer.Ordinal);

        public string? SessionId { get; set; }

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
