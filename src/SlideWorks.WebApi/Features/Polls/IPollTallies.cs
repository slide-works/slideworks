using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// Each round's running tally, in memory (V5, D16). The answer writer calls ApplyAsync only after its commit. A round first
// touched after a restart is loaded from its answer rows, whichever call touches it first. A singleton: a call that needs
// the database opens its own scope.
public interface IPollTallies
{
    // One guest's answer changed from oldPicks to newPicks, and the commit that wrote it is done. oldPicks is empty for the
    // guest's first answer in the round, which is what raises Answered. The answer writer reads a round (ReadAsync) before it
    // commits to it, so the round is loaded before its rows exist and each later call adds its delta exactly once. On a
    // round that is not loaded yet, this call loads it from the answer rows and does not add its own delta, since the rows
    // already hold that answer; a delta from another commit already in those rows would still be added on top, which is why
    // the writer loads first. Then every Changed handler is awaited in order, so a handler must only note the change (the
    // pump marks the deck dirty) and return, never publish inline.
    Task ApplyAsync(PollRoundRef round, IReadOnlyList<int> oldPicks, IReadOnlyList<int> newPicks);

    // The round's tally, with one count per option for optionCount options (padded after an option was added). Read only
    // while the round is open: a closed round's result is its Final snapshot, which IPolls.ControlAsync returns.
    Task<PollCounts> ReadAsync(string roundId, int optionCount);

    // Calls handler with the round after each change ApplyAsync makes, until the subscription is disposed.
    IDisposable Changed(Func<PollRoundRef, Task> handler);
}
