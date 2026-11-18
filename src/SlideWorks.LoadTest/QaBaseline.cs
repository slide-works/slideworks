namespace SlideWorks.LoadTest;

// What the qa guests are counted from, read once before the run: the round, and the votes each question had. A guest's
// upvote on question n is in a tally once the tally's votes for n reach Votes[n] plus the upvotes on n acknowledged up to
// and including it; Acknowledge hands that number out.
public sealed class QaBaseline(string round, IReadOnlyDictionary<int, int> votes)
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _acknowledged = [];

    public string Round { get; } = round;

    // The open questions the guests choose from, by number.
    public IReadOnlyList<int> Open { get; } = [.. votes.Keys];

    public int Acknowledge(int number)
    {
        lock (_lock)
        {
            var acknowledged = _acknowledged.GetValueOrDefault(number) + 1;
            _acknowledged[number] = acknowledged;
            return votes[number] + acknowledged;
        }
    }
}
