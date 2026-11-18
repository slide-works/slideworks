using System.Text.Json;
using SlideWorks.LoadTest;

namespace SlideWorks.UnitTest.LoadTest;

[TestFixture]
public class QaGuestTallyTests
{
    private const string ROUND = "round-1";

    private RunState _state = null!;
    private QaGuest _guest = null!;

    [SetUp]
    public void SetUp()
    {
        var options = LoadOptions.Parse(["--code", "123456", "--scenario", "qa", "--results-timeout", "0.05"]);
        _state = new RunState(0);
        _guest = new QaGuest(options, new Uri("http://localhost:5510"), new HttpClient(), _state, new QaTotals(), new QaBaseline(ROUND, new Dictionary<int, int>()));
    }

    [TearDown]
    public async Task TearDown() => await _guest.DisposeAsync();

    [Test]
    public async Task OnAudienceEvent_WhenTallyReachesTheExpectedVotes_ShouldReceiveTheTally()
    {
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());

        _guest.OnAudienceEvent(Tally(ROUND, (7, 5)));
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsReceived.ShouldBe(1);
        _state.ResultsTimedOut.ShouldBe(0);
    }

    [Test]
    public async Task OnAudienceEvent_WhenTallyIsOneVoteShort_ShouldTimeOut()
    {
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());

        _guest.OnAudienceEvent(Tally(ROUND, (7, 4)));
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsReceived.ShouldBe(0);
        _state.ResultsTimedOut.ShouldBe(1);
    }

    [Test]
    public async Task OnAudienceEvent_WhenTallyArrivedBeforeTheUpvoteWasAwaited_ShouldCountItAsZeroMilliseconds()
    {
        _guest.OnAudienceEvent(Tally(ROUND, (7, 6)));
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsReceived.ShouldBe(1);
        _state.ResultsMs.ShouldBe([0]);
    }

    [Test]
    public async Task OnAudienceEvent_WhenTallyIsForAnotherRound_ShouldBeIgnored()
    {
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());

        _guest.OnAudienceEvent(Tally("round-2", (7, 50)));
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsTimedOut.ShouldBe(1);
    }

    [Test]
    public async Task OnAudienceEvent_WhenTallyIsForAnotherQuestion_ShouldBeIgnored()
    {
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());

        _guest.OnAudienceEvent(Tally(ROUND, (8, 50)));
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsTimedOut.ShouldBe(1);
    }

    [Test]
    public async Task OnAudienceEvent_WhenEventIsNotAQuestionTally_ShouldBeIgnored()
    {
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());

        _guest.OnAudienceEvent(Parse($$"""{"type":"pollTally","round":"{{ROUND}}","votes":[{"number":7,"votes":50}]}"""));
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsTimedOut.ShouldBe(1);
    }

    [Test]
    public async Task OnAudienceEvent_WhenALaterTallyShowsFewerVotes_ShouldKeepTheHighestSeen()
    {
        _guest.OnAudienceEvent(Tally(ROUND, (7, 6)));
        _guest.OnAudienceEvent(Tally(ROUND, (7, 2)));
        _guest.Await(7, 5, System.Diagnostics.Stopwatch.GetTimestamp());
        await _guest.WaitForTalliesAsync(CancellationToken.None);

        _state.ResultsReceived.ShouldBe(1);
    }

    private static JsonElement Tally(string round, params (int Number, int Votes)[] votes) =>
        Parse($$"""{"type":"questionTally","round":"{{round}}","votes":[{{string.Join(",", votes.Select(v => $$"""{"number":{{v.Number}},"votes":{{v.Votes}}}"""))}}]}""");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
