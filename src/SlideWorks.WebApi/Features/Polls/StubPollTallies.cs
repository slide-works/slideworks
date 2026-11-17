using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// TODO: replace with the real in-memory tallies, loaded from the answer rows and raising Changed after each Apply.
// Every round reads as no answers and a zero count per option; Apply changes nothing and Changed never fires.
[RegisterSingleton(For = typeof(IPollTallies))]
public sealed class StubPollTallies : IPollTallies
{
    public void Apply(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks)
    {
        // Nothing is counted until the real tallies land.
    }

    public Task<PollCounts> ReadAsync(string roundId, int optionCount) => Task.FromResult(new PollCounts(0, new int[optionCount]));

    public IDisposable Changed(Func<PollRoundRef, Task> handler) => new NoSubscription();

    private sealed class NoSubscription : IDisposable
    {
        public void Dispose()
        {
            // Nothing to stop: the stub never calls the handler.
        }
    }
}
