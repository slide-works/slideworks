namespace SlideWorks.LoadTest;

// The scenarios --scenario names. The names are the command line's; Parse has already refused any other.
public static class LoadScenario
{
    public const string POLL = "poll";
    public const string QA = "qa";

    public static readonly IReadOnlyList<string> Names = [POLL, QA];

    public static ILoadScenario Create(LoadOptions options, HttpClient http) => options.Scenario == QA
        ? new QaScenario(options, http)
        : new PollScenario(options, http);
}
