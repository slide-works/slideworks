using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IPollTallies for tests that need a tally without answers: each test sets the counts a round reads as, and a round with
// none set reads as no answers and a zero per option. Apply records the change and starts calling every Changed handler
// with the round, in order, without waiting for them; it does not change the counts. A test that needs the handlers done
// calls RaiseChangedAsync instead, which awaits each. Add it with FakePollTallies.AddTo.
public sealed class FakePollTallies : IPollTallies
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PollCounts> _counts = new(StringComparer.Ordinal);
    private readonly List<(PollRoundRef Round, IReadOnlyList<int> OldPicks, IReadOnlyList<int> NewPicks)> _applied = [];
    private ImmutableList<Func<PollRoundRef, Task>> _handlers = [];

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

    public void Apply(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks)
    {
        lock (_gate)
        {
            _applied.Add((round, oldPicks, newPicks));
        }
        _ = RaiseChangedAsync(round);
    }

    public async Task RaiseChangedAsync(PollRoundRef round)
    {
        foreach (var handler in _handlers)
        {
            await handler(round);
        }
    }

    public Task<PollCounts> ReadAsync(string roundId, int optionCount)
    {
        lock (_gate)
        {
            return Task.FromResult(_counts.GetValueOrDefault(roundId) ?? new PollCounts(0, new int[optionCount]));
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
