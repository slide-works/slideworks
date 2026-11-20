using System.Globalization;

namespace SlideWorks.LoadTest;

// The command line: --scenario, --url, --code, --voters (--guests), --seconds, --pick, and --results-timeout (how long a voter
// waits for a tally that includes its answer before it counts as not received). Parse throws ArgumentException with a message
// for the user.
public sealed record LoadOptions(
    Uri Url,
    string Code,
    int Voters,
    double Seconds,
    string Pick,
    double ResultsTimeoutSeconds,
    bool NoResults)
{
    // The presenter (Presenter.cs): the cookie of someone who can present the deck, the deck's id and the poll's 1-based
    // slide. All three or none. With them the harness moves the presentation to the poll slide and probes during the run.
    public string? OwnerCookie { get; init; }

    public string? Deck { get; init; }

    public int Slide { get; init; }

    // The origins voters' hub connections are spread over, round robin: the same app under several addresses, so a run of
    // thousands of voters is not capped by the client machine's ephemeral ports per destination. Defaults to Url.
    public IReadOnlyList<Uri> HubUrls { get; init; } = [];

    // What the guests do (LoadScenario): poll, the default and the run as it always was, or qa.
    public string Scenario { get; init; } = LoadScenario.POLL;

    // qa only: how many questions the harness asks (one per guest of its own) before the run, so the guests have some to
    // upvote. 0 runs against the questions the round already holds.
    public int Questions { get; init; } = DEFAULT_QUESTIONS;

    // Seconds into the answer window at which the presenter closes the poll, or null to leave it open.
    public double? CloseAt { get; init; }

    public const int MAX_JOINS_IN_FLIGHT = 50;

    public const int DEFAULT_QUESTIONS = 5;

    public const string NO_RESULTS = "--no-results";

    public const string USAGE =
        "Usage: SlideWorks.LoadTest --code <join code> [--scenario poll|qa] [--url http://localhost:5510] [--voters 100] [--seconds 10]\n"
        + "                           [--pick random|<n>[,<n>...]] [--results-timeout 10] [--no-results]\n"
        + "  --scenario qa has each guest (--guests is --voters) read the questions, upvote three open ones and tap ten reactions, and\n"
        + "  gates the writes' p95, the p95 from an upvote's 200 to the first questionTally that includes it, and errors. [--questions 5]\n"
        + "  is how many questions the harness asks first (0 uses the ones the round has). qa refuses --pick, --owner-cookie, --deck,\n"
        + "  --slide and --close-at; poll refuses --questions.\n"
        + "  --pick takes 0-based option positions; random (the default) picks one option per voter.\n"
        + "  --no-results says tallies are not expected (a run against the stub routes, or a poll whose results are not live): the\n"
        + "  results check is skipped. Without it, a poll whose results are not live is refused and a results p95 of n/a fails the gate.\n"
        + "  [--owner-cookie <cookie> --deck <id> --slide <n> [--close-at <s>]] puts the presenter on the poll slide, probes a\n"
        + "  presenter move, a player's first join and a guest's join during the run, and closes the poll --close-at seconds in.\n"
        + "  [--hub-urls <url>,<url>...] spreads the voters' hub connections over several origins of the same app.\n"
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
        var unknown = values.Keys.Except(["--url", "--code", "--voters", "--seconds", "--pick", "--results-timeout", "--scenario", "--guests", "--questions", "--owner-cookie", "--deck", "--slide", "--close-at", "--hub-urls"]).ToList();
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
        var scenario = values.GetValueOrDefault("--scenario", LoadScenario.POLL);
        if (!LoadScenario.Names.Contains(scenario))
        {
            throw new ArgumentException($"--scenario must be {string.Join(" or ", LoadScenario.Names)}.");
        }
        if (values.ContainsKey("--voters") && values.ContainsKey("--guests"))
        {
            throw new ArgumentException("--voters and --guests are the same option: give one.");
        }
        var forScenario = scenario == LoadScenario.QA
            ? new[] { "--pick", "--owner-cookie", "--deck", "--slide", "--close-at" }
            : ["--questions"];
        var refused = forScenario.FirstOrDefault(values.ContainsKey);
        if (refused != null)
        {
            throw new ArgumentException($"{refused} is not an option of --scenario {scenario}.");
        }
        var voters = Whole(values, values.ContainsKey("--guests") ? "--guests" : "--voters", 100);
        var seconds = Positive(values, "--seconds", 10);
        var timeout = Positive(values, "--results-timeout", 10);
        var pick = values.GetValueOrDefault("--pick", "random");
        PickChoice.Validate(pick);
        var owner = values.GetValueOrDefault("--owner-cookie");
        var deck = values.GetValueOrDefault("--deck");
        var hasSlide = values.ContainsKey("--slide");
        if ((owner != null || deck != null || hasSlide) && (owner == null || deck == null || !hasSlide))
        {
            throw new ArgumentException("--owner-cookie, --deck and --slide go together.");
        }
        if (values.ContainsKey("--close-at") && owner == null)
        {
            throw new ArgumentException("--close-at needs --owner-cookie, --deck and --slide.");
        }
        return new LoadOptions(url, code.Trim(), voters, seconds, pick, timeout, noResults)
        {
            OwnerCookie = owner,
            Deck = deck,
            Scenario = scenario,
            Questions = values.ContainsKey("--questions") ? Count(values, "--questions") : DEFAULT_QUESTIONS,
            Slide = hasSlide ? Whole(values, "--slide", 1) : 0,
            CloseAt = values.ContainsKey("--close-at") ? Positive(values, "--close-at", 1) : null,
            HubUrls = values.TryGetValue("--hub-urls", out var hubUrls)
                ? hubUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Origin).ToList()
                : [url],
        };
    }

    private static Uri Origin(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : throw new ArgumentException($"--hub-urls: '{text}' is not an absolute http or https URL.");

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

    private static int Count(Dictionary<string, string> values, string name) =>
        int.TryParse(values[name], CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw new ArgumentException($"{name} must be a whole number, zero or more.");

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
