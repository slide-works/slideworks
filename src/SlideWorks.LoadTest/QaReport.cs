using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SlideWorks.LoadTest;

// What a qa run found: the table for a person and the JSON line for a script. Percentiles are null (n/a) with no samples. The
// gate is the poll's (LoadReport's limits): write p95 under 1 s, the p95 from an upvote's 200 to the first questionTally that
// includes it under 2 s, errors under 1%. It is only a gate at 1,000 guests; the verdict is printed for every run. "Served"
// says what each route answered, so a run against stubs or half-landed services says so.
public sealed record QaReport(
    int Guests,
    double Seconds,
    int Joined,
    int QuestionsAsked,
    int OpenQuestions,
    int Votes,
    int Reactions,
    double? WriteP50Ms,
    double? WriteP95Ms,
    double? WriteP99Ms,
    double? TallyP95Ms,
    int TalliesReceived,
    int TalliesTimedOut,
    int Dropped,
    int Errors,
    double ErrorRate,
    IReadOnlyDictionary<string, int> ErrorsByKind,
    IReadOnlyDictionary<string, int> Served,
    bool GatePassed,
    IReadOnlyList<string> GateFailures) : IRunReport
{
    public static QaReport From(LoadOptions options, RunState state, QaTotals totals)
    {
        var write = state.WriteMs;
        var errors = state.ErrorCount();
        var writeP95 = Percentile.Of(write, 95);
        // As the poll's: with no tally received, or more timed out than received one, the percentile would mostly be the
        // timeout itself and read as a latency, so n/a, beside the counts. n/a fails the gate unless --no-results.
        var tallyP95 = state.ResultsReceived == 0 || state.ResultsTimedOut > state.ResultsReceived ? null : Percentile.Of(state.ResultsMs, 95);
        var errorRate = errors / (double)options.Voters;
        var failures = Failures(options, writeP95, tallyP95, errorRate);
        return new QaReport(
            options.Voters,
            options.Seconds,
            state.Joined,
            totals.QuestionsAsked,
            totals.OpenQuestions,
            totals.Votes,
            totals.Reactions,
            Percentile.Of(write, 50),
            writeP95,
            Percentile.Of(write, 99),
            tallyP95,
            state.ResultsReceived,
            state.ResultsTimedOut,
            state.Dropped,
            errors,
            errorRate,
            state.ErrorsByKind(),
            totals.Outcomes(),
            failures.Count == 0,
            failures);
    }

    private static List<string> Failures(LoadOptions options, double? writeP95, double? tallyP95, double errorRate)
    {
        var failures = new List<string>();
        if (writeP95 is not < LoadReport.WRITE_P95_LIMIT_MS)
        {
            failures.Add($"write p95 {Ms(writeP95)} ms is not under {LoadReport.WRITE_P95_LIMIT_MS:F0} ms");
        }
        if (!options.NoResults && tallyP95 is not < LoadReport.RESULTS_P95_LIMIT_MS)
        {
            failures.Add($"tally p95 {Ms(tallyP95)} ms is not under {LoadReport.RESULTS_P95_LIMIT_MS:F0} ms (n/a is a failure unless {LoadOptions.NO_RESULTS})");
        }
        if (errorRate >= LoadReport.ERROR_RATE_LIMIT)
        {
            failures.Add($"error rate {errorRate.ToString("P2", CultureInfo.InvariantCulture)} is not under {LoadReport.ERROR_RATE_LIMIT:P0}");
        }
        return failures;
    }

    public string Table()
    {
        var text = new StringBuilder();
        text.AppendLine($"scenario qa   guests {Guests}   joined {Joined}   window {Seconds.ToString(CultureInfo.InvariantCulture)} s");
        text.AppendLine($"questions    {QuestionsAsked} asked by the harness, {OpenQuestions} open when the run started");
        text.AppendLine($"writes       {Votes} upvotes, {Reactions} reactions");
        text.AppendLine($"write ms     p50 {Ms(WriteP50Ms)}   p95 {Ms(WriteP95Ms)}   p99 {Ms(WriteP99Ms)}");
        text.AppendLine($"tally ms     p95 {Ms(TallyP95Ms)}   ({TalliesReceived} received, {TalliesTimedOut} timed out; a timeout counts at its full wait)");
        text.AppendLine($"errors       {Errors} ({ErrorRate.ToString("P2", CultureInfo.InvariantCulture)}), of which {Dropped} dropped connections");
        foreach (var (kind, count) in ErrorsByKind.OrderByDescending(e => e.Value))
        {
            text.AppendLine($"  {count,6}  {kind}");
        }
        text.AppendLine("served       " + (Served.Count == 0 ? "nothing" : string.Join(", ", Served.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key} x{s.Value}"))));
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
