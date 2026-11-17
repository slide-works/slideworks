namespace SlideWorks.WebApi.Features.Polls;

// What IRoundAnswerWriter.ReplaceAsync returns (SW-9 Q3), a closed set: the private constructor means only the three cases
// below exist. Nothing is written for Stale or Closed.
public abstract record RoundWrite
{
    private RoundWrite()
    {
    }

    // The answer committed: its picks, ascending, and its new revision.
    public sealed record Written(IReadOnlyList<int> Picks, int Revision) : RoundWrite;

    // The round is open but the guest's revision was no longer the expected one when its group committed, which includes a
    // second answer from the same guest in one group.
    public sealed record Stale() : RoundWrite;

    // The round was not open when its group committed: closed, never opened, or unknown.
    public sealed record Closed() : RoundWrite;
}
