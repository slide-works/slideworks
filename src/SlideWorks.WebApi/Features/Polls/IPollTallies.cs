using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// Each round's running tally, in memory (V5, D16). The answer writer applies a change only after its commit. A round read
// for the first time after a restart is loaded from its answer rows, and its counts are padded to the option count the
// caller passes when an option was added. A singleton: a call that needs the database opens its own scope.
public interface IPollTallies
{
    // One guest's answer changed from oldPicks to newPicks. oldPicks is empty for the guest's first answer in the round.
    void Apply(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks);

    // The round's tally, with one count per option for optionCount options.
    Task<PollCounts> ReadAsync(string roundId, int optionCount);

    // Calls handler with the round after each change Apply makes, until the subscription is disposed.
    IDisposable Changed(Func<PollRoundRef, Task> handler);
}
