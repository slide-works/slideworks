using System.Threading.Channels;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;

namespace SlideWorks.WebApi.Features.Polls;

// Writes answers in groups (D16): every answer goes on one in-memory queue, and a single loop commits whatever is queued, up
// to MAX_GROUP answers, in one transaction. It starts the next group as soon as one commits, so an answer waits at most for
// the commit in flight, and under a burst the queue fills while a commit runs and the groups grow with the load.
//
// Inside each transaction (SQLite's BEGIN IMMEDIATE, so no other write runs beside it) the round is read again: an answer to
// a round that is closed, or not opened, by then is Closed and nothing of it is written. Closing a round is one write
// transaction too, so an answer is either in the round's Final or refused. The revision is checked again there as well, in
// queue order, so two answers from one guest in one group can't both replace the same revision: the second is Stale. An
// answer is replaced, revision + 1, never deleted. Every kind of round goes through it the same way (SW-9 Q3): it is
// IRoundAnswerWriter, which IPollAnswers calls for a slide poll and SW-9's asking and voting seams call for a Q&A round.
//
// Before the transaction begins, the writer reads each of the group's rounds from the tally, so a round is loaded from its
// rows before this group writes any (LoadTalliesAsync). Only after the commit does the tally change: IPollTallies.ApplyAsync
// once per written answer, in order, then each waiter gets its result. A group that fails to commit fails every waiter in it, and nothing of it is applied. Disposing the writer
// (the host shutting down) stops the loop and fails whatever is still queued, so no waiter hangs.
//
// One instance is both AnswerWriter (SW-6's writer tests queue on it directly) and IRoundAnswerWriter, through Seam: two
// plain registrations would make two writers, each with its own queue and loop.
[RegisterSingleton(Concrete = true)]
[RegisterSingleton(For = typeof(IRoundAnswerWriter), Factory = typeof(Seam))]
public sealed class AnswerWriter : IRoundAnswerWriter, IDisposable
{
    public const int MAX_GROUP = 200;

    private readonly IServiceScopeFactory _scopes;
    private readonly IPollTallies _tallies;
    private readonly TimeProvider _clock;
    private readonly ILogger<AnswerWriter> _log;
    // Unbounded: every queued answer is a request already waiting on it, from a guest on the roster within its 30 a minute,
    // so the queue is bounded by the room; a bound would only turn a burst into refusals.
    private readonly Channel<Queued> _queue = Channel.CreateUnbounded<Queued>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stopping = new();

    public AnswerWriter(IServiceScopeFactory scopes, IPollTallies tallies, TimeProvider clock, ILogger<AnswerWriter> log)
    {
        _scopes = scopes;
        _tallies = tallies;
        _clock = clock;
        _log = log;
        // The loop runs for the writer's life and ends itself once stopped, failing its waiters; nothing awaits it.
        _ = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
    }

    // Queues the guest's answer to the round, its picks sorted ascending, and completes once its group has committed: Written
    // with the answer as written (its picks and new revision), Closed when the round was not open at commit time, or Stale when the guest's revision
    // was no longer expectedRevision. Throws what the group's commit threw when it failed.
    public Task<RoundWrite> ReplaceAsync(PollRoundRef round, string guestId, IReadOnlyList<int> picks, int expectedRevision)
    {
        var queued = new Queued(round, Guid.Parse(round.RoundId), guestId, expectedRevision, [.. picks.Order()]);
        if (!_queue.Writer.TryWrite(queued))
        {
            throw new ObjectDisposedException(nameof(AnswerWriter), "The answer writer has stopped.");
        }
        return queued.Done.Task;
    }

    // SW-6's form of ReplaceAsync, which its writer tests call: the answer as written, or null when it was Stale or Closed.
    // TODO: only SW-6's writer tests call this; it goes when they move to ReplaceAsync.
    public async Task<MyAnswer?> WriteAsync(PollRoundRef round, string guestId, int expectedRevision, IReadOnlyList<int> picks)
        => await ReplaceAsync(round, guestId, picks, expectedRevision) is RoundWrite.Written written
            ? new MyAnswer(written.Picks, written.Revision)
            : null;

    // Stops taking answers and cancels the loop, which fails the group it holds and everything still queued, then ends. It
    // doesn't wait for the loop: a commit in flight is cancelled, and its waiters fail too.
    public void Dispose()
    {
        if (!_queue.Writer.TryComplete())
        {
            return;
        }
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        var group = new List<Queued>(MAX_GROUP);
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stopping))
            {
                while (group.Count < MAX_GROUP && _queue.Reader.TryRead(out var next))
                {
                    group.Add(next);
                }
                await CommitAsync(group, stopping);
                group.Clear();
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Stopping: the waiters still held are failed below.
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "The answer writer's loop stopped");
        }
        var stopped = new ObjectDisposedException(nameof(AnswerWriter), "The answer writer has stopped.");
        foreach (var queued in group)
        {
            queued.Done.TrySetException(stopped);
        }
        _queue.Writer.TryComplete();
        FailQueued(stopped);
    }

    private void FailQueued(Exception reason)
    {
        while (_queue.Reader.TryRead(out var queued))
        {
            queued.Done.TrySetException(reason);
        }
    }

    // Commits one group. Never throws: a failed commit fails the group's waiters.
    private async Task CommitAsync(List<Queued> all, CancellationToken stopping)
    {
        var group = await LoadTalliesAsync(all);
        if (group.Count == 0)
        {
            return;
        }
        List<Written> written;
        HashSet<Guid> open;
        try
        {
            (written, open) = await WriteGroupAsync(group, stopping);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "An answer group of {Count} failed to commit", group.Count);
            foreach (var queued in group)
            {
                queued.Done.TrySetException(ex);
            }
            return;
        }
        foreach (var write in written)
        {
            try
            {
                // Its Changed handlers only mark the deck dirty (the pump publishes on its own timer), so this can't stall the
                // loop on a publish.
                await _tallies.ApplyAsync(write.Queued.Round, write.OldPicks, write.Answer.Picks);
            }
            catch (Exception ex)
            {
                // The answer is committed, so it is the guest's answer whatever the tally does; the tally rebuilds from the
                // rows after a restart.
                _log.LogError(ex, "The tally of round {RoundId} did not take an answer that committed", write.Queued.Round.RoundId);
            }
        }
        foreach (var write in written)
        {
            write.Queued.Done.TrySetResult(write.Answer);
        }
        foreach (var queued in group)
        {
            // Every answer not written was refused at commit time: its round wasn't open, or its revision had moved on.
            queued.Done.TrySetResult(open.Contains(queued.RoundId) ? new RoundWrite.Stale() : new RoundWrite.Closed());
        }
    }

    // Reads each of the group's rounds from the tally before any of its rows is written, outside the write transaction. A
    // round the tally has not loaded yet is loaded now, from rows that hold none of this group's answers, so every answer
    // applied after the commit is a delta on top of an exact count: a load triggered by the first ApplyAsync would already
    // hold the whole group and count the rest twice, as would a read that first touched the round between commit and apply.
    // This loop is the only writer of answer rows and the tally keeps an open round once loaded, so nothing can slip in
    // between. A loaded round costs an in-memory read: no query. When a round's read throws (its load failed), that round's
    // waiters get the exception and nothing of it is written, with no retry: the next answer's read loads it again. The
    // group's other rounds go ahead. Returns the answers whose rounds were read.
    private async Task<List<Queued>> LoadTalliesAsync(List<Queued> group)
    {
        var failed = new HashSet<Guid>();
        foreach (var round in group.GroupBy(q => q.RoundId).Select(g => g.First().Round))
        {
            try
            {
                await _tallies.ReadAsync(round.RoundId, round.OptionCount);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "The tally of round {RoundId} could not be read before its answers were written", round.RoundId);
                failed.Add(Guid.Parse(round.RoundId));
                foreach (var queued in group.Where(q => q.Round.RoundId == round.RoundId))
                {
                    queued.Done.TrySetException(ex);
                }
            }
        }
        return failed.Count == 0 ? group : [.. group.Where(q => !failed.Contains(q.RoundId))];
    }

    // The answers written, and the group's rounds that were open inside the transaction.
    private async Task<(List<Written> Written, HashSet<Guid> Open)> WriteGroupAsync(List<Queued> group, CancellationToken stopping)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(stopping);

        var roundIds = group.Select(q => q.RoundId).Distinct().ToList();
        var open = (await db.PollRounds.AsNoTracking()
            .Where(r => roundIds.Contains(r.Id) && r.OpenedAt != null && r.ClosedAt == null)
            .Select(r => r.Id)
            .ToListAsync(stopping)).ToHashSet();
        var guestIds = group.Where(q => open.Contains(q.RoundId)).Select(q => q.GuestId).Distinct().ToList();
        var current = await db.PollAnswers
            .Where(a => open.Contains(a.RoundId) && guestIds.Contains(a.GuestId))
            .ToDictionaryAsync(a => (a.RoundId, a.GuestId), stopping);

        var now = _clock.GetUtcNow();
        var written = new List<Written>();
        foreach (var queued in group.Where(q => open.Contains(q.RoundId)))
        {
            var key = (queued.RoundId, queued.GuestId);
            var answer = current.GetValueOrDefault(key);
            if ((answer?.Revision ?? 0) != queued.ExpectedRevision)
            {
                continue;
            }
            IReadOnlyList<int> oldPicks = answer is null ? [] : [.. answer.Picks];
            if (answer is null)
            {
                answer = new PollAnswer { RoundId = queued.RoundId, GuestId = queued.GuestId, Picks = [.. queued.Picks], Revision = 1, UpdatedAt = now };
                db.PollAnswers.Add(answer);
                current[key] = answer;
            }
            else
            {
                answer.Picks = [.. queued.Picks];
                answer.Revision++;
                answer.UpdatedAt = now;
            }
            written.Add(new Written(queued, oldPicks, new RoundWrite.Written(queued.Picks, answer.Revision)));
        }
        if (written.Count > 0)
        {
            await db.SaveChangesAsync(stopping);
        }
        await transaction.CommitAsync(stopping);
        return (written, open);
    }

    private sealed record Queued(PollRoundRef Round, Guid RoundId, string GuestId, int ExpectedRevision, IReadOnlyList<int> Picks)
    {
        public TaskCompletionSource<RoundWrite> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record Written(Queued Queued, IReadOnlyList<int> OldPicks, RoundWrite.Written Answer);

    // Resolves IRoundAnswerWriter to the app's one AnswerWriter.
    public sealed class Seam : IComponentFactory<IRoundAnswerWriter>
    {
        public IRoundAnswerWriter Build(IComponentResolver c) => c.Resolve<AnswerWriter>();
    }
}
