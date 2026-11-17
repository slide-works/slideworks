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
    int Dropped,
    int Errors,
    double ErrorRate,
    IReadOnlyDictionary<string, int> ErrorsByKind,
    bool GatePassed,
    IReadOnlyList<string> GateFailures)
{
    // The gate (D8, V10): write p95 under 1 s, results p95 under 2 s, errors under 1%. It is only a gate at 1,000 voters; the
    // larger runs are recorded, but the verdict is printed for every run. A results p95 of n/a fails unless tallies are not
    // expected (--no-results).
    public const double WRITE_P95_LIMIT_MS = 1000;
    public const double RESULTS_P95_LIMIT_MS = 2000;
    public const double ERROR_RATE_LIMIT = 0.01;

    public static LoadReport From(LoadOptions options, RunState state)
    {
        var write = state.WriteMs;
        var results = state.ResultsMs;
        var errors = state.ErrorCount();
        var writeP95 = Percentile.Of(write, 95);
        // With no tally received, or more voters timed out than received one, the percentile would mostly be the timeout
        // itself and read as a latency: n/a instead, beside the received and timed-out counts. n/a fails the gate.
        var resultsP95 = state.ResultsReceived == 0 || state.ResultsTimedOut > state.ResultsReceived ? null : Percentile.Of(results, 95);
        var errorRate = errors / (double)options.Voters;
        var failures = Failures(options, writeP95, resultsP95, errorRate);
        return new LoadReport(
            options.Voters,
            options.Seconds,
            state.Joined,
            state.Answered,
            Percentile.Of(write, 50),
            writeP95,
            Percentile.Of(write, 99),
            resultsP95,
            state.ResultsReceived,
            state.ResultsTimedOut,
            state.Dropped,
            errors,
            errorRate,
            state.ErrorsByKind(),
            failures.Count == 0,
            failures);
    }

    private static List<string> Failures(LoadOptions options, double? writeP95, double? resultsP95, double errorRate)
    {
        var failures = new List<string>();
        if (writeP95 is not < WRITE_P95_LIMIT_MS)
        {
            failures.Add($"write p95 {Ms(writeP95)} ms is not under {WRITE_P95_LIMIT_MS:F0} ms");
        }
        if (!options.NoResults && resultsP95 is not < RESULTS_P95_LIMIT_MS)
        {
            failures.Add($"results p95 {Ms(resultsP95)} ms is not under {RESULTS_P95_LIMIT_MS:F0} ms (n/a is a failure unless {LoadOptions.NO_RESULTS})");
        }
        if (errorRate >= ERROR_RATE_LIMIT)
        {
            failures.Add($"error rate {errorRate.ToString("P2", CultureInfo.InvariantCulture)} is not under {ERROR_RATE_LIMIT:P0}");
        }
        return failures;
    }

    public string Table()
    {
        var text = new StringBuilder();
        text.AppendLine($"voters {Voters}   joined {Joined}   answered {Answered}   window {Seconds.ToString(CultureInfo.InvariantCulture)} s");
        text.AppendLine($"write ms     p50 {Ms(WriteP50Ms)}   p95 {Ms(WriteP95Ms)}   p99 {Ms(WriteP99Ms)}");
        text.AppendLine($"results ms   p95 {Ms(ResultsP95Ms)}   ({ResultsReceived} received, {ResultsTimedOut} timed out; a timeout counts at its full wait)");
        text.AppendLine($"errors       {Errors} ({ErrorRate.ToString("P2", CultureInfo.InvariantCulture)}), of which {Dropped} dropped connections");
        foreach (var (kind, count) in ErrorsByKind.OrderByDescending(e => e.Value))
        {
            text.AppendLine($"  {count,6}  {kind}");
        }
        text.AppendLine(GatePassed ? "GATE PASS" : "GATE FAIL");
        foreach (var failure in GateFailures)
        {
            text.AppendLine($"  {failure}");
        }
        return text.ToString();
    }

    public string Json() => JsonSerializer.Serialize(this, PollWire.JSON);

    private static string Ms(double? value) => value is null ? "n/a" : value.Value.ToString("F0", CultureInfo.InvariantCulture);
}
