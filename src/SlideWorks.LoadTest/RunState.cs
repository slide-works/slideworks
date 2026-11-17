namespace SlideWorks.LoadTest;

// What every voter shares: the answered count the round had before the run, the number of answers acknowledged so far, the
// samples, and the errors by kind.
public sealed class RunState(int baselineAnswered)
{
    private readonly object _lock = new();
    private readonly List<double> _writeMs = [];
    private readonly List<double> _resultsMs = [];
    private readonly Dictionary<string, int> _errors = new(StringComparer.Ordinal);
    private int _acknowledged;
    private int _resultsTimedOut;
    private int _dropped;

    public int BaselineAnswered { get; } = baselineAnswered;

    public int Joined { get; private set; }

    public int Answered { get; private set; }

    public IReadOnlyList<double> WriteMs => Snapshot(_writeMs);

    // Results latencies that arrived; a voter whose wait ran out is counted in ResultsTimedOut and in these at the timeout.
    public IReadOnlyList<double> ResultsMs => Snapshot(_resultsMs);

    public int ResultsReceived { get; private set; }

    public int ResultsTimedOut { get { lock (_lock) { return _resultsTimedOut; } } }

    public IReadOnlyDictionary<string, int> ErrorsByKind()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_errors);
        }
    }

    // Voters whose hub connection failed to start, or closed before their tally arrived. Each is also one error, once.
    public int Dropped { get { lock (_lock) { return _dropped; } } }

    public int ErrorCount() => ErrorsByKind().Values.Sum();

    public void RecordJoined() { lock (_lock) { Joined++; } }

    // The voter's answer got its 200. Returns the tally the voter should wait for: the baseline plus every answer
    // acknowledged up to and including this one, which all committed before they were acknowledged (V5).
    public int RecordWrite(double ms)
    {
        lock (_lock)
        {
            Answered++;
            _writeMs.Add(ms);
            return BaselineAnswered + ++_acknowledged;
        }
    }

    public void RecordResults(double ms)
    {
        lock (_lock)
        {
            ResultsReceived++;
            _resultsMs.Add(ms);
        }
    }

    public void RecordResultsTimeout(double timeoutMs)
    {
        lock (_lock)
        {
            _resultsTimedOut++;
            _resultsMs.Add(timeoutMs);
        }
    }

    public void RecordDropped()
    {
        lock (_lock)
        {
            _dropped++;
        }
    }

    public void RecordError(string kind)
    {
        lock (_lock)
        {
            _errors[kind] = _errors.GetValueOrDefault(kind) + 1;
        }
    }

    private List<double> Snapshot(List<double> samples)
    {
        lock (_lock)
        {
            return [.. samples];
        }
    }
}
