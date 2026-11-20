using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IPollTallies for tests that need a tally without answers: each test sets the counts a round reads as, and a round with
// none set reads as no answers and a zero per option. ApplyAsync records the change, then awaits every Changed handler with
// the round, in order, as the real one does; it does not change the counts. BlockNextRead holds the next ReadAsync until
// ReleaseRead, for a test of two overlapping reads. Add it with FakePollTallies.AddTo. The app, and so this fake, is shared
// by every test in a fixture: each test uses round ids of its own.
public sealed class FakePollTallies : IPollTallies
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PollCounts> _counts = new(StringComparer.Ordinal);
    private readonly List<(PollRoundRef Round, IReadOnlyList<int> OldPicks, IReadOnlyList<int> NewPicks)> _applied = [];
    private ImmutableList<Func<PollRoundRef, Task>> _handlers = [];
    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _release;

    public IReadOnlyList<(PollRoundRef Round, IReadOnlyList<int> OldPicks, IReadOnlyList<int> NewPicks)> Applied
    {
        get
        {
            lock (_gate)
            {
                return [.. _applied];
            }
        }
    }

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakePollTallies>().AddSingleton<IPollTallies>(provider => provider.GetRequiredService<FakePollTallies>());

    public void Set(string roundId, PollCounts counts)
    {
        lock (_gate)
        {
            _counts[roundId] = counts;
        }
    }

    // The next ReadAsync waits until ReleaseRead; the returned task completes once that read has started waiting.
    public Task BlockNextRead()
    {
        lock (_gate)
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }
    }

    public void ReleaseRead()
    {
        lock (_gate)
        {
            _release?.TrySetResult();
            _release = null;
        }
    }

    public async Task ApplyAsync(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks, bool hadText = false, bool hasText = false)
    {
        lock (_gate)
        {
            _applied.Add((round, oldPicks, newPicks));
        }
        foreach (var handler in _handlers)
        {
            await handler(round);
        }
    }

    public async Task<PollCounts> ReadAsync(string roundId, int optionCount)
    {
        Task? hold = null;
        lock (_gate)
        {
            if (_entered != null && _release != null)
            {
                _entered.TrySetResult();
                hold = _release.Task;
                _entered = null;
            }
        }
        if (hold != null)
        {
            await hold;
        }
        lock (_gate)
        {
            return _counts.GetValueOrDefault(roundId) ?? new PollCounts(0, new int[optionCount]);
        }
    }

    public IDisposable Changed(Func<PollRoundRef, Task> handler)
    {
        ImmutableInterlocked.Update(ref _handlers, list => list.Add(handler));
        return new Subscription(() => ImmutableInterlocked.Update(ref _handlers, list => list.Remove(handler)));
    }

    private sealed class Subscription(Action remove) : IDisposable
    {
        public void Dispose() => remove();
    }
}
