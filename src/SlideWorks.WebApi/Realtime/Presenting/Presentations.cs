using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Presenting.Views;

namespace SlideWorks.WebApi.Realtime.Presenting;

// The presentations registry (P2, P4, P7, P10): one state per deck, in memory in this one process (D9). Each deck's state
// is changed only under that deck's lock; the lock is also held while the connection joins or leaves present:{deckId}
// and while the new state is sent there, so the group receives states in seq order. Observers run after the lock is let
// go. Every check that can refuse a call runs before anything changes, so a refused call changes, sends and notifies
// nothing. A state with no connections is kept for KEEP_IDLE and dropped by the next call that finds it (no timer).
//
// Every duration here (the running talk timer, KEEP_IDLE, the started label's age) is measured between monotonic
// TimeProvider timestamps (GetTimestamp/GetElapsedTime), never as a difference of wall-clock times, so a wall-clock step
// (NTP, a VM or WSL clock sync) neither adds nor removes time. The wall clock is read only for clockMinutes.
//
// SignalR runs OnDisconnectedAsync (LeaveAllAsync) without waiting for the connection's hub calls still in flight, so a
// join can still be on its way (in the hub's access check or the deck read) when its connection's leave has already
// run. LeaveAllAsync therefore marks the connection closed, and a join adds its entry only if, in the same step under
// _indexLock, it finds the connection not closed and indexes it. Either the join is refused before anything changes, or
// it is indexed first and the leave then finds and removes it. Marks are pruned after KEEP_CLOSED, timed with the
// monotonic TimeProvider timestamp so a wall-clock jump neither prunes them early nor keeps them longer.
[RegisterSingleton(For = typeof(IPresentations))]
public sealed class Presentations(
    IHubContext<DeckHub> hub,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IEnumerable<IPresentationObserver> observers,
    ILogger<Presentations> logger) : IPresentations
{
    public const int LABEL_MAX = 60;

    public static readonly TimeSpan KEEP_IDLE = TimeSpan.FromHours(2);

    // How long a closed connection stays marked, which bounds how long its in-flight join may take to be refused.
    public static readonly TimeSpan KEEP_CLOSED = TimeSpan.FromMinutes(10);

    // JoinAsync's refusal for a connection that closed while the join was in flight. Nobody receives it.
    public const string CONNECTION_CLOSED = "Connection closed";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Live> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImmutableHashSet<string>> _decksByConnection = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    private readonly Queue<(string ConnectionId, long ClosedAt)> _closedOrder = new();
    private readonly Lock _indexLock = new();
    private readonly IPresentationObserver[] _observers = observers.ToArray();

    public async Task<PresentationJoined> JoinAsync(
        string connectionId,
        string deckId,
        string clientId,
        PresentationRole role,
        string label,
        string timeZone,
        PresentationResume? resume)
    {
        var facts = await DeckFactsAsync(deckId);
        PresentationState state;
        await using (await LockAsync(deckId))
        {
            if (!TryIndex(connectionId, deckId))
            {
                throw new HubException(CONNECTION_CLOSED);
            }
            var now = clock.GetTimestamp();
            var live = Current(deckId, now) ?? Create(deckId, facts, resume, now);
            foreach (var replaced in live.Join(connectionId, clientId, role, Label(label)))
            {
                await hub.Groups.RemoveFromGroupAsync(replaced, DeckHub.PresentationGroupName(deckId));
                Unindex(replaced, deckId);
            }
            await hub.Groups.AddToGroupAsync(connectionId, DeckHub.PresentationGroupName(deckId));
            state = await AcceptAsync(live, now);
        }
        await NotifyAsync(state);
        return new PresentationJoined(state, PresentationClock.MinutesSinceMidnight(clock.GetUtcNow(), timeZone));
    }

    public async Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update)
    {
        var facts = await DeckFactsAsync(deckId);
        PresentationState state;
        await using (await LockAsync(deckId))
        {
            var now = clock.GetTimestamp();
            var live = Joined(connectionId, deckId, facts, now);
            live.Move(update, facts!.Steps, now);
            state = await AcceptAsync(live, now);
        }
        await NotifyAsync(state);
        return state;
    }

    public async Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command)
    {
        var facts = await DeckFactsAsync(deckId);
        PresentationState state;
        await using (await LockAsync(deckId))
        {
            var now = clock.GetTimestamp();
            var live = Joined(connectionId, deckId, facts, now);
            live.Time(command, now);
            state = await AcceptAsync(live, now);
        }
        await NotifyAsync(state);
        return state;
    }

    public async Task LeaveAsync(string connectionId, string deckId)
    {
        PresentationState? state = null;
        await using (await LockAsync(deckId))
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, DeckHub.PresentationGroupName(deckId));
            Unindex(connectionId, deckId);
            var now = clock.GetTimestamp();
            var live = Current(deckId, now);
            if (live != null && live.Leave(connectionId, now))
            {
                state = await AcceptAsync(live, now);
            }
        }
        if (state != null)
        {
            await NotifyAsync(state);
        }
    }

    public async Task LeaveAllAsync(string connectionId)
    {
        ImmutableHashSet<string>? decks;
        lock (_indexLock)
        {
            MarkClosed(connectionId);
            _decksByConnection.TryGetValue(connectionId, out decks);
        }
        foreach (var deckId in decks ?? [])
        {
            await LeaveAsync(connectionId, deckId);
        }
    }

    public async Task<LivePresentationsView> ListLiveAsync(Func<string, Task<bool>> canPresent)
    {
        var now = clock.GetTimestamp();
        var snapshots = new List<(PresentationState State, long CreatedAt)>();
        foreach (var deckId in _states.Keys)
        {
            await using (await LockAsync(deckId))
            {
                var live = Current(deckId, now);
                if (live != null && live.HasWindow)
                {
                    snapshots.Add((live.Snapshot(now), live.CreatedAt));
                }
            }
        }
        if (snapshots.Count == 0)
        {
            return new LivePresentationsView([]);
        }
        var ids = new List<string>();
        foreach (var deckId in snapshots.Select(s => s.State.DeckId).Distinct(StringComparer.Ordinal))
        {
            if (await canPresent(deckId))
            {
                ids.Add(deckId);
            }
        }
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var decks = await db.Decks.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Title, Total = db.Slides.Count(s => s.DeckId == d.Id && s.DeletedAt == null) })
            .ToDictionaryAsync(d => d.Id, StringComparer.Ordinal);
        return new LivePresentationsView(snapshots
            .Where(s => decks.ContainsKey(s.State.DeckId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new LivePresentation(
                s.State.DeckId,
                decks[s.State.DeckId].Title,
                s.State.Slide,
                decks[s.State.DeckId].Total,
                StartedLabel(clock.GetElapsedTime(s.CreatedAt, now)),
                s.State.Connected.Select(c => new LiveWindow(c.Role, c.Label)).ToList()))
            .ToList());
    }

    public async Task TalkLengthChangedAsync(string deckId, int? minutes)
    {
        if (!_states.ContainsKey(deckId))
        {
            return;
        }
        PresentationState? state = null;
        await using (await LockAsync(deckId))
        {
            var now = clock.GetTimestamp();
            var live = Current(deckId, now);
            if (live != null)
            {
                live.TalkMinutes = minutes;
                state = await AcceptAsync(live, now);
            }
        }
        if (state != null)
        {
            await NotifyAsync(state);
        }
    }

    public async Task<PresentationState?> GetAsync(string deckId)
    {
        if (!_states.ContainsKey(deckId))
        {
            return null;
        }
        await using (await LockAsync(deckId))
        {
            var now = clock.GetTimestamp();
            return _states.TryGetValue(deckId, out var live) && !live.Expired(now) ? live.Snapshot(now) : null;
        }
    }

    public async Task<PresentationState> StartAsync(string deckId)
    {
        var facts = await DeckFactsAsync(deckId);
        PresentationState state;
        await using (await LockAsync(deckId))
        {
            var now = clock.GetTimestamp();
            var existing = Current(deckId, now);
            if (existing != null)
            {
                return existing.Snapshot(now);
            }
            state = await AcceptAsync(Create(deckId, facts, resume: null, now), now);
        }
        await NotifyAsync(state);
        return state;
    }

    // "started just now" under a minute, then whole minutes, then hours and minutes.
    public static string StartedLabel(TimeSpan age)
    {
        var minutes = (int)Math.Max(0, age.TotalMinutes);
        return minutes switch
        {
            < 1 => "started just now",
            < 60 => $"started {minutes} min ago",
            _ when minutes % 60 == 0 => $"started {minutes / 60} h ago",
            _ => $"started {minutes / 60} h {minutes % 60} min ago",
        };
    }

    private static string Label(string? label)
    {
        var trimmed = (label ?? "").Trim();
        return trimmed.Length > LABEL_MAX ? trimmed[..LABEL_MAX].TrimEnd() : trimmed;
    }

    // The deck's current state, dropping it first when it has sat with no connections for KEEP_IDLE. Under the lock.
    private Live? Current(string deckId, long now)
    {
        if (!_states.TryGetValue(deckId, out var live))
        {
            return null;
        }
        if (!live.Expired(now))
        {
            return live;
        }
        _states.TryRemove(deckId, out _);
        return null;
    }

    // A new epoch from resume (clamped, its timer kept as sent), or at slide 1, step 0, timer notStarted. Under the lock.
    private Live Create(string deckId, DeckFacts? facts, PresentationResume? resume, long now)
    {
        var live = new Live(clock, deckId, NewEpoch(), facts?.TalkMinutes, now);
        if (resume != null)
        {
            live.Resume(resume, facts?.Steps ?? [], now);
        }
        _states[deckId] = live;
        return live;
    }

    // The state this connection may change: refused with NOT_PRESENTING, before anything changes, when the deck is gone,
    // has no state, or this connection holds no entry in it. Under the lock.
    private Live Joined(string connectionId, string deckId, DeckFacts? facts, long now)
    {
        var live = facts == null ? null : Current(deckId, now);
        if (live == null || !live.Holds(connectionId))
        {
            throw new HubException(DeckHub.NOT_PRESENTING);
        }
        return live;
    }

    // Raises seq and sends the state to the group. Under the lock.
    private async Task<PresentationState> AcceptAsync(Live live, long now)
    {
        live.Seq++;
        var state = live.Snapshot(now);
        await hub.Clients.Group(DeckHub.PresentationGroupName(live.DeckId)).SendAsync(DeckHub.PRESENTATION_STATE_METHOD, state);
        return state;
    }

    // Outside the lock: each observer in turn; one that throws is logged and skipped.
    private async Task NotifyAsync(PresentationState state)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnChangedAsync(state);
            }
            catch (Exception e)
            {
                logger.LogError(e, "A presentation observer {Observer} threw for deck {DeckId}", observer.GetType().Name, state.DeckId);
            }
        }
    }

    // The connection -> decks index LeaveAllAsync walks. A connection with no decks left has no row. False, with nothing
    // indexed, when the connection has closed.
    private bool TryIndex(string connectionId, string deckId)
    {
        lock (_indexLock)
        {
            if (_closed.Contains(connectionId))
            {
                return false;
            }
            _decksByConnection[connectionId] = _decksByConnection.TryGetValue(connectionId, out var decks) ? decks.Add(deckId) : [deckId];
            return true;
        }
    }

    // Under _indexLock: marks the connection closed and prunes the marks older than KEEP_CLOSED (monotonic timestamps).
    private void MarkClosed(string connectionId)
    {
        while (_closedOrder.TryPeek(out var oldest) && clock.GetElapsedTime(oldest.ClosedAt) >= KEEP_CLOSED)
        {
            _closedOrder.Dequeue();
            _closed.Remove(oldest.ConnectionId);
        }
        if (_closed.Add(connectionId))
        {
            _closedOrder.Enqueue((connectionId, clock.GetTimestamp()));
        }
    }

    private void Unindex(string connectionId, string deckId)
    {
        lock (_indexLock)
        {
            if (!_decksByConnection.TryGetValue(connectionId, out var decks))
            {
                return;
            }
            var left = decks.Remove(deckId);
            if (left.IsEmpty)
            {
                _decksByConnection.Remove(connectionId);
            }
            else
            {
                _decksByConnection[connectionId] = left;
            }
        }
    }

    private async Task<Releaser> LockAsync(string deckId)
    {
        var gate = _locks.GetOrAdd(deckId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        return new Releaser(gate);
    }

    // The deck's owner-independent facts the registry needs: its talk length and each live slide's step count, in order.
    // Null when the deck no longer exists.
    private async Task<DeckFacts?> DeckFactsAsync(string deckId)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.TalkMinutes }).SingleOrDefaultAsync();
        if (deck == null)
        {
            return null;
        }
        var steps = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .Select(s => s.Steps)
            .ToListAsync();
        return new DeckFacts(deck.TalkMinutes, steps);
    }

    private static string NewEpoch() => $"p-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant()}";

    private sealed record DeckFacts(int? TalkMinutes, IReadOnlyList<int> Steps);

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }

    // One connection's entry in connected (P1). Order is its join order; a replacing join keeps the entry's place.
    private sealed record Entry(string ConnectionId, PresentationConnection Connection, long Order);

    // One deck's mutable state. Touched only under the deck's lock. Every instant it keeps (createdAt, the timer's start,
    // the idle mark) is a monotonic TimeProvider timestamp, and every duration is measured between two of them.
    private sealed class Live(TimeProvider clock, string deckId, string epoch, int? talkMinutes, long createdAt)
    {
        private readonly List<Entry> _entries = [];
        private long _nextOrder;
        private int _slide = 1;
        private int _step;
        private bool _black;
        private bool _skipDeepDives;
        private TimerState _timer = TimerState.NotStarted;
        private long _bankedMs;
        private long _runningSince;
        private long? _idleSince = createdAt;

        public string DeckId { get; } = deckId;

        public long CreatedAt { get; } = createdAt;

        public long Seq { get; set; }

        public int? TalkMinutes { get; set; } = talkMinutes;

        public bool HasWindow => _entries.Exists(e => e.Connection.Role is PresentationRole.Presenter or PresentationRole.Audience);

        public bool Expired(long now) => _entries.Count == 0 && _idleSince is { } idleSince && clock.GetElapsedTime(idleSince, now) >= KEEP_IDLE;

        public bool Holds(string connectionId) => _entries.Exists(e => e.ConnectionId == connectionId);

        // Adds this connection's entry, replacing one with the same clientId (in its place) and any other entry this
        // connection held. Returns the other connections whose entry was replaced, so they leave the group.
        public IReadOnlyList<string> Join(string connectionId, string clientId, PresentationRole role, string label)
        {
            var replaced = _entries.Where(e => e.Connection.ClientId == clientId || e.ConnectionId == connectionId).ToList();
            var order = replaced.FirstOrDefault(e => e.Connection.ClientId == clientId)?.Order ?? _nextOrder++;
            _entries.RemoveAll(replaced.Contains);
            _entries.Add(new Entry(connectionId, new PresentationConnection(clientId, role, label), order));
            _idleSince = null;
            return replaced.Select(e => e.ConnectionId).Where(c => c != connectionId).Distinct().ToList();
        }

        // Removes the entry this connection holds, if it still holds one. True when something was removed.
        public bool Leave(string connectionId, long now)
        {
            if (_entries.RemoveAll(e => e.ConnectionId == connectionId) == 0)
            {
                return false;
            }
            if (_entries.Count == 0)
            {
                _idleSince = now;
            }
            return true;
        }

        public void Resume(PresentationResume resume, IReadOnlyList<int> steps, long now)
        {
            (_slide, _step) = Clamp(resume.Slide, resume.Step, steps);
            _black = resume.Black;
            _skipDeepDives = resume.SkipDeepDives;
            _timer = resume.Timer.State;
            _bankedMs = _timer == TimerState.NotStarted ? 0 : Math.Max(0, resume.Timer.ElapsedMs);
            _runningSince = now;
        }

        public void Move(PresentationUpdate update, IReadOnlyList<int> steps, long now)
        {
            var (slide, step) = Clamp(update.Slide, update.Step, steps);
            if ((slide, step) != (_slide, _step) && _timer == TimerState.NotStarted)
            {
                Start(now);
            }
            (_slide, _step) = (slide, step);
            _black = update.Black;
            _skipDeepDives = update.SkipDeepDives;
        }

        // pause stops a running timer; resume starts a paused or not-started one; reset goes back to 0 and notStarted.
        public void Time(TimerCommand command, long now)
        {
            switch (command)
            {
                case TimerCommand.Pause when _timer == TimerState.Running:
                    _bankedMs = ElapsedMs(now);
                    _timer = TimerState.Paused;
                    break;
                case TimerCommand.Resume when _timer != TimerState.Running:
                    _runningSince = now;
                    _timer = TimerState.Running;
                    break;
                case TimerCommand.Reset:
                    _bankedMs = 0;
                    _timer = TimerState.NotStarted;
                    break;
            }
        }

        public PresentationState Snapshot(long now) => new(
            DeckId,
            epoch,
            Seq,
            _slide,
            _step,
            _black,
            _skipDeepDives,
            new PresentationTimer(_timer, ElapsedMs(now)),
            TalkMinutes,
            _entries.OrderBy(e => RoleOrder(e.Connection.Role)).ThenBy(e => e.Order).Select(e => e.Connection).ToList());

        private static int RoleOrder(PresentationRole role) => role switch
        {
            PresentationRole.Presenter => 0,
            PresentationRole.Audience => 1,
            _ => 2,
        };

        // Slide 1..N and step 0..that slide's steps, against the deck's current slides.
        private static (int Slide, int Step) Clamp(int slide, int step, IReadOnlyList<int> steps)
        {
            var clampedSlide = Math.Clamp(slide, 1, Math.Max(1, steps.Count));
            var maxStep = steps.Count == 0 ? 0 : Math.Max(0, steps[clampedSlide - 1]);
            return (clampedSlide, Math.Clamp(step, 0, maxStep));
        }

        private void Start(long now)
        {
            _bankedMs = 0;
            _runningSince = now;
            _timer = TimerState.Running;
        }

        private long ElapsedMs(long now)
            => _timer == TimerState.Running ? _bankedMs + (long)clock.GetElapsedTime(_runningSince, now).TotalMilliseconds : _bankedMs;
    }
}
