using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SlideWorks.LoadTest;

// What a run found: the table for a person and the JSON line for a script. Percentiles are null (n/a) with no samples.
public sealed record LoadReport(
    int Voters,
    double Seconds,
    int Joined,
    int Answered,
    double? WriteP50Ms,
    double? WriteP95Ms,
    double? WriteP99Ms,
    double? ResultsP95Ms,
    int ResultsReceived,
    int ResultsTimedOut,
    int Errors,
    double ErrorRate,
    IReadOnlyDictionary<string, int> ErrorsByKind)
{
    public static LoadReport From(LoadOptions options, RunState state)
    {
        var write = state.WriteMs;
        var results = state.ResultsMs;
        var errors = state.ErrorCount();
        return new LoadReport(
            options.Voters,
            options.Seconds,
            state.Joined,
            state.Answered,
            Percentile.Of(write, 50),
            Percentile.Of(write, 95),
            Percentile.Of(write, 99),
            // With no tally received at all there is nothing to measure: n/a, not the timeout.
            state.ResultsReceived == 0 ? null : Percentile.Of(results, 95),
            state.ResultsReceived,
            state.ResultsTimedOut,
            errors,
            errors / (double)options.Voters,
            state.ErrorsByKind());
    }

    public string Table()
    {
        var text = new StringBuilder();
        text.AppendLine($"voters {Voters}   joined {Joined}   answered {Answered}   window {Seconds.ToString(CultureInfo.InvariantCulture)} s");
        text.AppendLine($"write ms     p50 {Ms(WriteP50Ms)}   p95 {Ms(WriteP95Ms)}   p99 {Ms(WriteP99Ms)}");
        text.AppendLine($"results ms   p95 {Ms(ResultsP95Ms)}   ({ResultsReceived} received, {ResultsTimedOut} timed out; a timeout counts at its full wait)");
        text.AppendLine($"errors       {Errors} ({ErrorRate.ToString("P2", CultureInfo.InvariantCulture)})");
        foreach (var (kind, count) in ErrorsByKind.OrderByDescending(e => e.Value))
        {
            text.AppendLine($"  {count,6}  {kind}");
        }
        return text.ToString();
    }

    public string Json() => JsonSerializer.Serialize(this, PollWire.JSON);

    private static string Ms(double? value) => value is null ? "n/a" : value.Value.ToString("F0", CultureInfo.InvariantCulture);
}
