using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// SW-8's open-response records against their samples in src/contracts/screens. Each sample is built or read into its record
// and written back, so the JSON must equal the sample exactly: names, nesting, value kinds, enum strings, and round left out
// in draft. The events are serialised through AudienceEvent, as the hub's relay does.
[TestFixture]
public class OpenResponseRecordShapeTests
{
    private const string DECK = "7Kq2xYp9LmQa";

    [Test]
    public void Record_WhenWordCloudIsWritten_ShouldMatchTheSample()
    {
        var example = Example("audience-events.json").GetProperty("wordCloud");

        var json = Serialise<AudienceEvent>(new WordCloudTally(
            DECK,
            OpenResponseTarget.Audience,
            example.GetProperty("slide").GetInt32(),
            Read<OpenResponsePhase>(example.GetProperty("phase")),
            example.GetProperty("round").GetString(),
            example.GetProperty("number").GetInt32(),
            example.GetProperty("revision").GetInt32(),
            example.GetProperty("revealed").GetBoolean(),
            example.GetProperty("guests").GetInt32(),
            Read<CloudWord[]>(example.GetProperty("words"))));

        JsonShape.AssertMatches(json, example, "audience-events.json wordCloud", ScreenShapes.OpenResponseEvent);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void Record_WhenOpenTextIsWritten_ShouldMatchTheSample()
    {
        var example = Example("audience-events.json").GetProperty("openText");

        var json = Serialise<AudienceEvent>(new OpenTextTally(
            DECK,
            OpenResponseTarget.Presenter,
            example.GetProperty("slide").GetInt32(),
            Read<OpenResponsePhase>(example.GetProperty("phase")),
            example.GetProperty("round").GetString(),
            example.GetProperty("number").GetInt32(),
            example.GetProperty("revision").GetInt32(),
            example.GetProperty("revealed").GetBoolean(),
            example.GetProperty("guests").GetInt32(),
            Read<WallAnswer[]>(example.GetProperty("answers"))));

        JsonShape.AssertMatches(json, example, "audience-events.json openText", ScreenShapes.OpenResponseEvent);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void Record_WhenTheRoundIsADraft_ShouldLeaveOutTheRoundAndNeverWriteTheDeckOrTarget()
    {
        var json = Serialise<AudienceEvent>(new WordCloudTally(DECK, OpenResponseTarget.Audience, 3, OpenResponsePhase.Draft, null, 1, 0, true, 0, []));

        json.GetRawText().ShouldBe("""{"type":"wordCloud","slide":3,"phase":"draft","number":1,"revision":0,"revealed":true,"guests":0,"words":[]}""");
    }

    [TestCase("word-cloud.json", "draft")]
    [TestCase("word-cloud.json", "open")]
    [TestCase("word-cloud.json", "closed")]
    [TestCase("open-text.json", "draft")]
    [TestCase("open-text.json", "open")]
    [TestCase("open-text.json", "closed")]
    public void Record_WhenGuestViewIsWritten_ShouldMatchTheSample(string file, string sample)
    {
        var example = Example(file).GetProperty(sample);

        var json = Serialise(Read<GuestResponseView>(example));

        JsonShape.AssertMatches(json, example, $"{file} {sample}", ScreenShapes.GuestResponse);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void Record_WhenWordsAreSubmitted_ShouldMatchTheSamples()
    {
        var request = Example("word-cloud.json").GetProperty("request");
        var stored = Example("word-cloud.json").GetProperty("stored");

        SameJson(Serialise(Read<WordsRequest>(request)), request).ShouldBeTrue(Differs(Serialise(Read<WordsRequest>(request)), request));
        SameJson(Serialise(Read<StoredWords>(stored)), stored).ShouldBeTrue(Differs(Serialise(Read<StoredWords>(stored)), stored));
        OpenTextRules.WordsOf(Read<WordsRequest>(request).Words).ShouldBe(Read<StoredWords>(stored).Words);
    }

    [Test]
    public void Record_WhenTextIsSubmitted_ShouldMatchTheSamples()
    {
        var request = Example("open-text.json").GetProperty("request");
        var stored = Example("open-text.json").GetProperty("stored");

        SameJson(Serialise(Read<TextRequest>(request)), request).ShouldBeTrue(Differs(Serialise(Read<TextRequest>(request)), request));
        SameJson(Serialise(Read<StoredText>(stored)), stored).ShouldBeTrue(Differs(Serialise(Read<StoredText>(stored)), stored));
        OpenTextRules.Collapse(Read<TextRequest>(request).Text).ShouldBe(Read<StoredText>(stored).Text);
    }

    [TestCase("draft")]
    [TestCase("wordCloud")]
    [TestCase("openText")]
    public void Record_WhenControlViewIsWritten_ShouldMatchTheSample(string sample)
    {
        var example = Example("open-response-control.json").GetProperty(sample);

        var json = Serialise(Read<OpenResponseControlView>(example));

        JsonShape.AssertMatches(json, example, $"open-response-control.json {sample}", ScreenShapes.OpenResponseControl);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    private static T Read<T>(JsonElement sample) => sample.Deserialize<T>(JsonSerializerOptions.Web)!;

    private static JsonElement Serialise<T>(T record) => JsonSerializer.SerializeToElement(record, JsonSerializerOptions.Web);

    private static JsonElement Example(string file) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
