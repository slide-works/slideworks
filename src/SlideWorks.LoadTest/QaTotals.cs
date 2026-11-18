namespace SlideWorks.LoadTest;

// What the qa scenario counts beyond RunState's samples: what the guests got from each route, so the report says which
// services the app served (real ones answer 200, 201 and 204; a stub or a missing route answers something else).
public sealed class QaTotals
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _outcomes = new(StringComparer.Ordinal);

    public int QuestionsAsked { get; set; }

    public int OpenQuestions { get; set; }

    public int Votes { get; private set; }

    public int Reactions { get; private set; }

    public void RecordVote() { lock (_lock) { Votes++; } }

    public void RecordReaction() { lock (_lock) { Reactions++; } }

    public void RecordOutcome(string route, int status)
    {
        lock (_lock)
        {
            var key = $"{route} {status}";
            _outcomes[key] = _outcomes.GetValueOrDefault(key) + 1;
        }
    }

    public IReadOnlyDictionary<string, int> Outcomes()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_outcomes);
        }
    }
}
