using System.Globalization;

namespace SlideWorks.LoadTest;

// The command line: --url, --code, --voters, --seconds, --pick, and --results-timeout (how long a voter waits for a tally
// that includes its answer before it counts as not received). Parse throws ArgumentException with a message for the user.
public sealed record LoadOptions(
    Uri Url,
    string Code,
    int Voters,
    double Seconds,
    string Pick,
    double ResultsTimeoutSeconds,
    bool NoResults)
{
    public const int MAX_JOINS_IN_FLIGHT = 50;

    public const string NO_RESULTS = "--no-results";

    public const string USAGE =
        "Usage: SlideWorks.LoadTest --code <join code> [--url http://localhost:5510] [--voters 100] [--seconds 10]\n"
        + "                           [--pick random|<n>[,<n>...]] [--results-timeout 10] [--no-results]\n"
        + "  --pick takes 0-based option positions; random (the default) picks one option per voter.\n"
        + "  --no-results says tallies are not expected (a run against the stub routes, or a poll whose results are not live): the\n"
        + "  results check is skipped. Without it, a poll whose results are not live is refused and a results p95 of n/a fails the gate.\n"
        + "  Exit codes: 0 gate passed, 3 gate failed, 1 poll not open or app unreachable, 2 bad option.";

    public static LoadOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var noResults = args.Contains(NO_RESULTS);
        var pairs = args.Where(a => a != NO_RESULTS).ToList();
        for (var i = 0; i < pairs.Count; i += 2)
        {
            if (!pairs[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= pairs.Count)
            {
                throw new ArgumentException($"Unexpected argument '{pairs[i]}'.");
            }
            values[pairs[i]] = pairs[i + 1];
        }
        var unknown = values.Keys.Except(["--url", "--code", "--voters", "--seconds", "--pick", "--results-timeout"]).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unknown option {unknown[0]}.");
        }
        var code = values.GetValueOrDefault("--code");
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("--code is required.");
        }
        if (!Uri.TryCreate(values.GetValueOrDefault("--url", "http://localhost:5510"), UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("--url must be an absolute http or https URL.");
        }
        var voters = Whole(values, "--voters", 100);
        var seconds = Positive(values, "--seconds", 10);
        var timeout = Positive(values, "--results-timeout", 10);
        var pick = values.GetValueOrDefault("--pick", "random");
        PickChoice.Validate(pick);
        return new LoadOptions(url, code.Trim(), voters, seconds, pick, timeout, noResults);
    }

    private static int Whole(Dictionary<string, string> values, string name, int fallback)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return fallback;
        }
        return int.TryParse(text, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new ArgumentException($"{name} must be a whole number above zero.");
    }

    private static double Positive(Dictionary<string, string> values, string name, double fallback)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return fallback;
        }
        return double.TryParse(text, CultureInfo.InvariantCulture, out var value) && value > 0 && double.IsFinite(value)
            ? value
            : throw new ArgumentException($"{name} must be a number above zero.");
    }
}
