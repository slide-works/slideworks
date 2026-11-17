using System.Collections.Concurrent;
using System.Collections.Immutable;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// Each open round's running tally, in memory (V5, D16): how many guests have answered and the count per option, by
// position. A round is loaded from its answer rows the first time anything touches it (a read, or an answer applied after
// a restart), once: two first touches that race share one load. After that it changes only through ApplyAsync, so no
// publish ever counts rows. The answer that triggers a load is already in the rows it loads, so its delta is not added
// on top.
//
// Only an open round stays in memory. A draft (no OpenedAt, which includes a discarded draft), a closed round or an
// unknown id is answered from the rows on each touch and not kept, so reads of those never grow memory. A round that has
// been kept but not touched for a while is dropped once its row says it has closed; that check runs at most once per
// interval, on the next load. A singleton: each load opens a scope of its own for the database.
[RegisterSingleton(For = typeof(IPollTallies))]
public sealed class PollTallies(IServiceScopeFactory scopes, TimeProvider time, ILogger<PollTallies> logger) : IPollTallies
{
    private static readonly TimeSpan _idleBeforeSweep = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, Task<RoundTally>> _rounds = new();
    private readonly Lock _sweepGate = new();
    private ImmutableList<Func<PollRoundRef, Task>> _handlers = [];
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    public async Task ApplyAsync(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks)
    {
        var (tally, loadedHere) = await TouchAsync(round.RoundId);
        if (!loadedHere)
        {
            tally.Apply(oldPicks, newPicks);
        }
        foreach (var handler in _handlers)
        {
            try
            {
                await handler(round);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The answer is committed and counted: a failing handler must neither fail it nor skip the others.
                logger.LogError(e, "A tally change handler failed for round {RoundId}", round.RoundId);
            }
        }
    }

    public async Task<PollCounts> ReadAsync(string roundId, int optionCount)
    {
        var (tally, _) = await TouchAsync(roundId);
        return tally.Read(optionCount);
    }

    public IDisposable Changed(Func<PollRoundRef, Task> handler)
    {
        ImmutableInterlocked.Update(ref _handlers, list => list.Add(handler));
        return new Subscription(() => ImmutableInterlocked.Update(ref _handlers, list => list.Remove(handler)));
    }

    // The round's tally, and whether this call loaded it from the rows. Whoever adds the round's entry runs the load; every
    // other first touch awaits the same load.
    private async Task<(RoundTally Tally, bool LoadedHere)> TouchAsync(string roundId)
    {
        if (!Guid.TryParse(roundId, out var id))
        {
            return (RoundTally.Empty(time.GetUtcNow()), true);
        }
        var load = new TaskCompletionSource<RoundTally>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = _rounds.GetOrAdd(id, load.Task);
        if (!ReferenceEquals(entry, load.Task))
        {
            var kept = await entry;
            kept.Touch(time.GetUtcNow());
            return (kept, false);
        }
        try
        {
            var (tally, open) = await LoadAsync(id);
            if (!open)
            {
                _rounds.TryRemove(new KeyValuePair<Guid, Task<RoundTally>>(id, load.Task));
            }
            load.SetResult(tally);
            return (tally, true);
        }
        catch (Exception e)
        {
            // The next touch loads again; everyone waiting on this load gets the failure.
            _rounds.TryRemove(new KeyValuePair<Guid, Task<RoundTally>>(id, load.Task));
            load.SetException(e);
            throw;
        }
    }

    // Counts the round's answer rows. Only a round with OpenedAt set counts; it is kept when it is still open.
    private async Task<(RoundTally Tally, bool Open)> LoadAsync(Guid id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var round = await db.PollRounds.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.OpenedAt, r.ClosedAt })
            .SingleOrDefaultAsync();
        var now = time.GetUtcNow();
        await SweepAsync(db, now);
        if (round?.OpenedAt is null)
        {
            return (RoundTally.Empty(now), false);
        }
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == id).Select(a => a.Picks).ToListAsync();
        return (RoundTally.From(answers, now), round.ClosedAt is null);
    }

    // Drops kept rounds that have not been touched for a while and whose rows say they have closed. Runs at most once per
    // interval, so a load costs one extra query now and then, never per answer or per publish.
    private async Task SweepAsync(AppDbContext db, DateTimeOffset now)
    {
        lock (_sweepGate)
        {
            if (now - _lastSweep < _idleBeforeSweep)
            {
                return;
            }
            _lastSweep = now;
        }
        var idle = new List<Guid>();
        foreach (var (id, loaded) in _rounds)
        {
            if (loaded.IsCompletedSuccessfully && now - (await loaded).TouchedAt >= _idleBeforeSweep)
            {
                idle.Add(id);
            }
        }
        if (idle.Count == 0)
        {
            return;
        }
        var closed = await db.PollRounds.AsNoTracking().Where(r => idle.Contains(r.Id) && r.ClosedAt != null).Select(r => r.Id).ToListAsync();
        foreach (var id in closed)
        {
            _rounds.TryRemove(id, out _);
        }
    }

    // One round's counts, changed under its own lock so concurrent applies and reads never see half a change.
    private sealed class RoundTally
    {
        private readonly Lock _gate = new();
        private readonly List<int> _counts;
        private int _answered;
        private DateTimeOffset _touchedAt;

        private RoundTally(int answered, List<int> counts, DateTimeOffset touchedAt)
        {
            _answered = answered;
            _counts = counts;
            _touchedAt = touchedAt;
        }

        public DateTimeOffset TouchedAt
        {
            get
            {
                lock (_gate)
                {
                    return _touchedAt;
                }
            }
        }

        public static RoundTally Empty(DateTimeOffset now) => new(0, [], now);

        // Answered is the number of guests with an answer row, not the number of picks: with pick several a guest's row
        // adds one to each option it picks.
        public static RoundTally From(IReadOnlyList<IReadOnlyList<int>> answers, DateTimeOffset now)
        {
            var tally = Empty(now);
            foreach (var picks in answers)
            {
                tally.Apply([], picks);
            }
            return tally;
        }

        public void Touch(DateTimeOffset now)
        {
            lock (_gate)
            {
                _touchedAt = now;
            }
        }

        // A first answer (no old picks) adds a guest; every old pick loses one and every new pick gains one. A pick past
        // the known options (one was appended) grows the list.
        public void Apply(IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks)
        {
            lock (_gate)
            {
                if (oldPicks.Count == 0 && newPicks.Count > 0)
                {
                    _answered++;
                }
                else if (oldPicks.Count > 0 && newPicks.Count == 0)
                {
                    _answered--;
                }
                foreach (var pick in oldPicks.Where(p => p < _counts.Count))
                {
                    _counts[pick]--;
                }
                foreach (var pick in newPicks)
                {
                    while (_counts.Count <= pick)
                    {
                        _counts.Add(0);
                    }
                    _counts[pick]++;
                }
            }
        }

        // Exactly one count per option for optionCount options: zero for an option appended since the last answer.
        public PollCounts Read(int optionCount)
        {
            lock (_gate)
            {
                var counts = new int[optionCount];
                for (var position = 0; position < optionCount && position < _counts.Count; position++)
                {
                    counts[position] = _counts[position];
                }
                return new PollCounts(_answered, counts);
            }
        }
    }

    private sealed class Subscription(Action remove) : IDisposable
    {
        public void Dispose() => remove();
    }
}
