using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// SW-6's poll records against their samples in src/contracts/screens (V4, V5, V9). Each sample is read into its record and
// written back, so the JSON must equal the sample exactly: names, nesting, value kinds and the camelCase enum strings. The
// events are serialised through AudienceEvent, as the hub's relay does.
[TestFixture]
public class PollRecordShapeTests
{
    private const string DECK = "7Kq2xYp9LmQa";

    [TestCase("draft")]
    [TestCase("open")]
    [TestCase("closed")]
    [TestCase("answerShown")]
    public void PollActivity_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("poll-activity.json").GetProperty(sample);

        var json = Serialise(Read<PollActivity>(example));

        JsonShape.AssertMatches(json, example, $"poll-activity.json {sample}", ScreenShapes.PollActivity);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void PollActivity_WhenBuilt_ShouldWriteTypePollFirstAndNoCountsOrHiddenSettings()
    {
        var activity = new PollActivity(null, 1, PollPhase.Draft, "Q", ["A", "B"], PollPick.One, null, PollResults.Live, true, null);

        var text = Serialise(activity).GetRawText();

        text.ShouldStartWith("{\"type\":\"poll\",", Case.Sensitive);
        text.ShouldContain("\"phase\":\"draft\"", Case.Sensitive);
        text.ShouldNotContain("counts", Case.Sensitive);
        text.ShouldNotContain("opens", Case.Sensitive);
        text.ShouldNotContain("privacy", Case.Sensitive);
    }

    [TestCase("answered")]
    [TestCase("hidden")]
    [TestCase("notAnswered")]
    [TestCase("noPoll")]
    [TestCase("notOpen")]
    public void GuestPollView_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("guest-poll.json").GetProperty(sample);

        var json = Serialise(GuestPollOf(example));

        JsonShape.AssertMatches(json, Example("guest-poll.json").GetProperty("answered"), $"guest-poll.json {sample}", ScreenShapes.GuestPoll);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void AnswerRequest_ShouldRoundTrip()
    {
        var example = Example("poll-answer.json").GetProperty("request");

        var request = Read<AnswerRequest>(example);

        request.Picks.ShouldBe([1]);
        request.Revision.ShouldBe(0);
        SameJson(Serialise(request), example).ShouldBeTrue(Differs(Serialise(request), example));
    }

    [Test]
    public void MyAnswer_ShouldMatchTheAcceptedBody()
    {
        var example = Example("poll-answer.json").GetProperty("accepted");

        var json = Serialise(new MyAnswer([1], 1));

        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void StaleAnswer_ShouldMatchTheConflictBody()
    {
        var example = Example("poll-answer.json").GetProperty("stale");

        var json = Serialise(new StaleAnswer(GuestPollOf(example.GetProperty("current"))));

        JsonShape.AssertMatches(json, example, "poll-answer.json stale", Prefixed("current.", ScreenShapes.GuestPoll));
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void PollAnswerFile_ShouldHoldEachErrorOfV5()
    {
        var example = Example("poll-answer.json");

        example.GetProperty("invalid").EnumerateArray().Select(e => e.GetProperty("error").GetString())
            .ShouldBe(["picks: pick one", "picks: at most 2", "picks: unknown option 7", "picks: duplicate", "picks: required"]);
        example.GetProperty("noGuest").GetProperty("error").GetString().ShouldBe("no_guest");
        example.GetProperty("noSession").GetProperty("error").GetString().ShouldBe("no_session");
        example.GetProperty("tooManyAnswers").GetProperty("error").GetString().ShouldBe("too_many_answers");
    }

    [TestCase("draft")]
    [TestCase("open")]
    [TestCase("closed")]
    public void PollControlView_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("poll-control.json").GetProperty(sample);

        var json = Serialise(Read<PollControlView>(example));

        JsonShape.AssertMatches(json, Example("poll-control.json").GetProperty("closed"), $"poll-control.json {sample}", ScreenShapes.PollControl);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void PollControlFile_ShouldHoldEachRefusalCodeOfV4()
    {
        var codes = Example("poll-control.json").GetProperty("refusals").EnumerateArray().Select(e => e.GetProperty("error").GetString());

        codes.ShouldBe([PollRefusals.NOT_LIVE, PollRefusals.WRONG_PHASE, PollRefusals.ANOTHER_POLL_OPEN, PollRefusals.ALREADY_VISIBLE, PollRefusals.NO_CORRECT]);
    }

    [Test]
    public void PollSettings_WhenDefault_ShouldWriteTheV1Defaults()
    {
        var json = Serialise(PollSettings.Default);

        json.GetRawText().ShouldBe("""{"pick":"one","max":null,"results":"live","correct":null,"opens":"onArrival","privacy":"private"}""");
    }

    [Test]
    public void PollResultsView_ShouldRoundTrip()
    {
        var example = Example("poll-results.json");

        var json = Serialise(Read<PollResultsView>(example));

        JsonShape.AssertMatches(json, "poll-results.json", ScreenShapes.PollResults);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void PollTally_ShouldMatchContract()
    {
        var example = Example("audience-events.json").GetProperty("pollTally");

        var json = Serialise<AudienceEvent>(TallyOf(example, PollTallyTarget.Results));

        JsonShape.AssertMatches(json, example, "audience-events.json pollTally", ScreenShapes.PollTally);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void PollTally_WhenCountsHidden_ShouldWriteNullCountsAndNeverTheDeckOrTarget()
    {
        var json = Serialise(new PollTally(DECK, PollTallyTarget.Audience, "r1", 21, null));

        json.GetRawText().ShouldBe("""{"type":"pollTally","round":"r1","answered":21,"counts":null}""");
    }

    [Test]
    public void PollControl_ShouldMatchContract()
    {
        var example = Example("audience-events.json").GetProperty("pollControl");

        var json = Serialise<AudienceEvent>(new PollControl(DECK, Read<PollControlView>(example.GetProperty("control"))));

        JsonShape.AssertMatches(json, example, "audience-events.json pollControl", Prefixed("control.", ScreenShapes.PollControl));
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    private static GuestPollView GuestPollOf(JsonElement sample)
    {
        var tally = sample.GetProperty("tally");
        return new GuestPollView(
            Read<PollActivity?>(sample.GetProperty("activity")),
            Read<MyAnswer?>(sample.GetProperty("mine")),
            tally.ValueKind == JsonValueKind.Null ? null : TallyOf(tally, PollTallyTarget.Audience));
    }

    private static PollTally TallyOf(JsonElement sample, PollTallyTarget target) => new(
        DECK,
        target,
        sample.GetProperty("round").GetString()!,
        sample.GetProperty("answered").GetInt32(),
        Read<int[]?>(sample.GetProperty("counts")));

    // The same paths one level down, for a record that holds the checked one as a property.
    private static JsonShapeOptions Prefixed(string prefix, JsonShapeOptions options) =>
        new() { NullablePaths = options.NullablePaths.Select(p => prefix + p).ToArray() };

    private static T Read<T>(JsonElement sample) => sample.Deserialize<T>(JsonSerializerOptions.Web)!;

    private static JsonElement Serialise<T>(T record) => JsonSerializer.SerializeToElement(record, JsonSerializerOptions.Web);

    private static JsonElement Example(string file) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
