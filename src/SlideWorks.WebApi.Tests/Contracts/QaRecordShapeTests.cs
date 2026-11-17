using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// SW-9's Q&A records against their samples in src/contracts/screens (Q1, Q4 to Q7, Q10). Each sample is read into its record
// and written back, so the JSON must equal the sample exactly: names, nesting, value kinds and the camelCase enum strings.
// The events are serialised through AudienceEvent, as the hub's relay does.
[TestFixture]
public class QaRecordShapeTests
{
    private const string DECK = "7Kq2xYp9LmQa";

    // Words that would tie a question or a vote to a guest (Q4, Q10).
    private static readonly string[] _guestWords = ["guest", "yours", "pick", "mine", "voter", "asker"];

    [TestCase("open")]
    [TestCase("closed")]
    [TestCase("offHere")]
    [TestCase("off")]
    public void QaRoom_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("qa-room.json").GetProperty(sample);

        var json = Serialise(Read<QaRoom>(example));

        JsonShape.AssertMatches(json, Example("qa-room.json").GetProperty("open"), $"qa-room.json {sample}", ScreenShapes.QaRoom);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QaRoom_WhenOffHere_ShouldWriteTheCamelCaseState()
    {
        var json = Serialise(new QaRoom(null, QaRoomQuestions.OffHere, true, false, 9));

        json.GetRawText().ShouldBe("""{"round":null,"questions":"offHere","names":true,"reactions":false,"listRevision":9}""");
    }

    [Test]
    public void AudienceStateRoom_ShouldBeTheOpenRoomSample()
    {
        var room = Example("audience-events.json").GetProperty("audienceState").GetProperty("room");

        var json = Serialise(Read<QaRoom>(room));

        SameJson(json, Example("qa-room.json").GetProperty("open")).ShouldBeTrue(Differs(json, room));
    }

    [TestCase("view")]
    [TestCase("ownHidden")]
    public void GuestQuestionsView_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("guest-questions.json").GetProperty(sample);

        var json = Serialise(Read<GuestQuestionsView>(example));

        JsonShape.AssertMatches(json, Example("guest-questions.json").GetProperty("view"), $"guest-questions.json {sample}", ScreenShapes.GuestQuestions);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void GuestQuestion_WhenOwnHidden_ShouldWriteHiddenAsTheState()
    {
        var question = new GuestQuestion(4, "Who reviews the slices?", null, "2 min", QuestionStatus.Hidden, 1, false, true);

        var text = Serialise(question).GetRawText();

        text.ShouldBe("""{"number":4,"text":"Who reviews the slices?","name":null,"age":"2 min","state":"hidden","votes":1,"onScreen":false,"yours":true}""");
    }

    [Test]
    public void GuestQuestionsFile_ShouldHoldTheGuestErrorsAndTheReactionBodiesOfQ7()
    {
        var example = Example("guest-questions.json");
        var reaction = example.GetProperty("reaction");

        example.GetProperty("guestErrors").EnumerateArray().Select(e => e.GetProperty("error").GetString())
            .ShouldBe(["no_guest", "no_session", "too_many_attempts"]);
        Read<ReactionRequest>(reaction.GetProperty("request")).Emoji.ShouldBe("clap");
        SameJson(Serialise(Read<ReactionRequest>(reaction.GetProperty("request"))), reaction.GetProperty("request")).ShouldBeTrue();
        reaction.GetProperty("invalid").GetProperty("error").GetString().ShouldBe(GuestQaErrors.UNKNOWN_EMOJI);
        reaction.GetProperty("reactionsOff").GetProperty("error").GetString().ShouldBe(GuestQaErrors.REACTIONS_OFF);
        reaction.GetProperty("tooManyReactions").GetProperty("error").GetString().ShouldBe(GuestQaErrors.TOO_MANY_REACTIONS);
    }

    [Test]
    public void AskRequest_ShouldRoundTrip()
    {
        var example = Example("question-ask.json").GetProperty("request");

        var request = Read<AskRequest>(example);

        request.ShouldBe(new AskRequest("How do you handle database migrations?", "Priya"));
        SameJson(Serialise(request), example).ShouldBeTrue(Differs(Serialise(request), example));
    }

    [Test]
    public void AskRequest_WhenAnonymous_ShouldWriteANullName()
    {
        var json = Serialise(new AskRequest("Can we get the slides?", null));

        JsonShape.AssertMatches(json, Example("question-ask.json").GetProperty("request"), "question-ask.json request", "name");
        json.GetRawText().ShouldBe("""{"text":"Can we get the slides?","name":null}""");
    }

    [Test]
    public void AskedQuestion_ShouldMatchTheCreatedBody()
    {
        var example = Example("question-ask.json").GetProperty("created");

        var json = Serialise(Read<AskedQuestion>(example));

        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        json.GetProperty("question").GetProperty("yours").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public void TooManyQuestions_ShouldMatchTheLimitedBody()
    {
        var example = Example("question-ask.json").GetProperty("tooManyQuestions");

        var json = Serialise(new TooManyQuestions(42));

        json.GetRawText().ShouldStartWith("{\"error\":\"too_many_questions\",", Case.Sensitive);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QuestionAskFile_ShouldHoldEachErrorOfQ4()
    {
        var example = Example("question-ask.json");

        example.GetProperty("invalid").EnumerateArray().Select(e => e.GetProperty("error").GetString())
            .ShouldBe(["text: required", "text: at most 200", "name: at most 30", "name: not allowed"]);
        example.GetProperty("refusals").EnumerateArray().Select(e => e.GetProperty("error").GetString())
            .ShouldBe([GuestQaErrors.QUESTIONS_CLOSED, GuestQaErrors.QUESTIONS_OFF, GuestQaErrors.QUESTIONS_OFF_HERE]);
    }

    [Test]
    public void QuestionAskFile_ShouldUseQuestionTextsOwnErrors()
    {
        var invalid = Example("question-ask.json").GetProperty("invalid").EnumerateArray().Select(e => e.GetProperty("error").GetString()).ToList();

        invalid.ShouldContain(QuestionText.Check(" ", null, true));
        invalid.ShouldContain(QuestionText.Check(new string('a', QuestionText.MAX_TEXT + 1), null, true));
        invalid.ShouldContain(QuestionText.Check("Why?", new string('n', QuestionText.MAX_NAME + 1), true));
        invalid.ShouldContain(QuestionText.Check("Why?", "Priya", false));
    }

    [Test]
    public void VoteRequest_ShouldRoundTrip()
    {
        var example = Example("question-votes.json").GetProperty("request");

        var request = Read<VoteRequest>(example);

        request.Picks.ShouldBe([1, 2, 4]);
        request.Revision.ShouldBe(3);
        SameJson(Serialise(request), example).ShouldBeTrue(Differs(Serialise(request), example));
    }

    [Test]
    public void MyVotes_ShouldMatchTheAcceptedBody()
    {
        var example = Example("question-votes.json").GetProperty("accepted");

        var json = Serialise(new MyVotes([1, 2, 4], 4));

        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void StaleVotes_ShouldMatchTheConflictBody()
    {
        var example = Example("question-votes.json").GetProperty("stale");

        var json = Serialise(new StaleVotes(Read<GuestQuestionsView>(example.GetProperty("current"))));

        JsonShape.AssertMatches(json, example, "question-votes.json stale", Prefixed("current.", ScreenShapes.GuestQuestions));
        json.GetRawText().ShouldStartWith("{\"error\":\"stale\",", Case.Sensitive);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QuestionVotesFile_ShouldHoldEachErrorOfQ4()
    {
        var example = Example("question-votes.json");

        example.GetProperty("invalid").EnumerateArray().Select(e => e.GetProperty("error").GetString())
            .ShouldBe(["picks: unknown question 7", "picks: duplicate"]);
        example.GetProperty("refusals").EnumerateArray().Select(e => e.GetProperty("error").GetString()).ShouldBe([GuestQaErrors.QUESTIONS_OFF]);
        example.GetProperty("tooManyVotes").GetProperty("error").GetString().ShouldBe(GuestQaErrors.TOO_MANY_VOTES);
    }

    [TestCase("live")]
    [TestCase("notLive")]
    public void QaControlView_ShouldRoundTripEachSample(string sample)
    {
        var example = Example("qa-control.json").GetProperty(sample);

        var json = Serialise(Read<QaControlView>(example));

        JsonShape.AssertMatches(json, Example("qa-control.json").GetProperty("live"), $"qa-control.json {sample}", ScreenShapes.QaControl);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QaControlView_WhenNotLive_ShouldHaveNoRoundNoQuestionsAndZeroCounts()
    {
        var view = Read<QaControlView>(Example("qa-control.json").GetProperty("notLive"));

        view.Live.ShouldBeFalse();
        view.Round.ShouldBeNull();
        view.Here.ShouldBeNull();
        view.Questions.ShouldBeEmpty();
        view.Counts.ShouldBe(new QaCounts(0, 0, 0, 0));
        view.Reactions.ShouldBe(ReactionCounts.Zero);
    }

    [Test]
    public void QaSettingsPatch_ShouldRoundTripTheSettingsRequest()
    {
        var example = Example("qa-control.json").GetProperty("settingsRequest");

        var patch = Read<QaSettingsPatch>(example);

        patch.ShouldBe(new QaSettingsPatch(QaQuestions.Closed, true, false, true, QaShowOn.QaSlide));
        SameJson(Serialise(patch), example).ShouldBeTrue(Differs(Serialise(patch), example));
    }

    [Test]
    public void QaSettingsPatch_WhenFieldsLeftOut_ShouldReadThemAsNull()
    {
        var patch = JsonSerializer.Deserialize<QaSettingsPatch>("""{"review":true}""", JsonSerializerOptions.Web)!;

        patch.ShouldBe(new QaSettingsPatch(null, true, null, null, null));
    }

    [Test]
    public void QaSlidePatch_ShouldRoundTripTheSlideRequest()
    {
        var example = Example("qa-control.json").GetProperty("slideRequest");

        var patch = Read<QaSlidePatch>(example);

        patch.ShouldBe(new QaSlidePatch(false, true));
        SameJson(Serialise(patch), example).ShouldBeTrue(Differs(Serialise(patch), example));
    }

    [Test]
    public void QaControlFile_ShouldHoldEachRefusalCodeOfQ6()
    {
        var codes = Example("qa-control.json").GetProperty("refusals").EnumerateArray().Select(e => e.GetProperty("error").GetString());

        codes.ShouldBe(["not_live", "wrong_state"]);
    }

    [Test]
    public void QaSettings_WhenDefault_ShouldWriteTheQ1Defaults()
    {
        var json = Serialise(QaSettings.Default);

        json.GetRawText().ShouldBe("""{"questions":"open","review":false,"names":true,"reactions":true,"showOn":"anySlide"}""");
    }

    [Test]
    public void QuestionsReport_ShouldRoundTrip()
    {
        var example = Example("questions-report.json");

        var json = Serialise(Read<QuestionsReport>(example));

        JsonShape.AssertMatches(json, "questions-report.json", ScreenShapes.QuestionsReport);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QuestionsReport_WhenSerialised_ShouldNeverSayWhichGuestAskedOrUpvoted()
    {
        var text = Example("questions-report.json").GetRawText();
        var written = Serialise(Read<QuestionsReport>(Example("questions-report.json"))).GetRawText();

        foreach (var word in new[] { "guest", "guestId", "yours", "picks", "mine" })
        {
            text.ShouldNotContain($"\"{word}\"", Case.Sensitive);
            written.ShouldNotContain($"\"{word}\"", Case.Sensitive);
        }
    }

    // The records themselves, not only the sample: a guest-id-like property added to any of them fails here, even before a
    // sample carries it.
    [TestCase(typeof(QuestionsReport))]
    [TestCase(typeof(QuestionsReportSession))]
    [TestCase(typeof(QuestionsReportQuestion))]
    public void QuestionsReportRecords_WhenReflected_ShouldHaveNoGuestOrVoterProperty(Type record)
    {
        var names = record.GetProperties().Select(p => p.Name);

        names.ShouldNotContain(name => _guestWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void QuestionTally_ShouldMatchContract()
    {
        var example = Example("audience-events.json").GetProperty("questionTally");

        var json = Serialise<AudienceEvent>(new QuestionTally(
            DECK,
            example.GetProperty("round").GetString()!,
            example.GetProperty("listRevision").GetInt64(),
            Read<QuestionVotes[]>(example.GetProperty("votes"))));

        JsonShape.AssertMatches(json, example, "audience-events.json questionTally", ScreenShapes.QuestionTally);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QuestionControl_ShouldMatchContract()
    {
        var example = Example("audience-events.json").GetProperty("questionControl");

        var json = Serialise<AudienceEvent>(new QuestionControl(DECK, Read<QaControlView>(example.GetProperty("control"))));

        JsonShape.AssertMatches(json, example, "audience-events.json questionControl", Prefixed("control.", ScreenShapes.QaControl));
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QuestionControl_ShouldCarryTheLiveControlSample()
    {
        var control = Example("audience-events.json").GetProperty("questionControl").GetProperty("control");

        SameJson(control, Example("qa-control.json").GetProperty("live")).ShouldBeTrue();
    }

    [Test]
    public void ReactionsBurst_ShouldMatchContract()
    {
        var example = Example("audience-events.json").GetProperty("reactions");

        var json = Serialise<AudienceEvent>(new ReactionsBurst(DECK, example.GetProperty("slide").GetInt32(), Read<ReactionCounts>(example.GetProperty("counts"))));

        JsonShape.AssertMatches(json, example, "audience-events.json reactions", ScreenShapes.ReactionsBurst);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void QaEvents_ShouldNeverWriteTheDeckId()
    {
        var control = Read<QaControlView>(Example("qa-control.json").GetProperty("notLive"));
        AudienceEvent[] events =
        [
            new QuestionTally(DECK, "r1", 3, [new QuestionVotes(1, 2)]),
            new QuestionControl(DECK, control),
            new ReactionsBurst(DECK, 2, ReactionCounts.Zero),
        ];

        foreach (var audienceEvent in events)
        {
            var json = Serialise(audienceEvent);
            json.GetRawText().ShouldNotContain(DECK, Case.Sensitive);
            json.TryGetProperty("deckId", out _).ShouldBeFalse();
        }
    }

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
