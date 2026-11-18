using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// The J4 audience events against src/contracts/screens/audience-events.json: each record is built from the example's own
// values and serialised through the base type, as the hub's relay does, so the JSON must equal the example exactly.
[TestFixture]
public class AudienceRecordShapeTests
{
    [Test]
    public void AudienceState_ShouldMatchContract()
    {
        var example = Example().GetProperty("audienceState");
        var current = example.GetProperty("current");
        AudienceEvent state = new AudienceState(
            "7Kq2xYp9LmQa",
            example.GetProperty("seq").GetInt64(),
            AudiencePhase.Live,
            new AudienceSlide(
                current.GetProperty("index").GetInt32(),
                current.GetProperty("count").GetInt32(),
                current.GetProperty("layout").GetString()!,
                current.GetProperty("slots").Clone(),
                current.GetProperty("steps").GetInt32(),
                current.GetProperty("step").GetInt32(),
                current.GetProperty("black").GetBoolean(),
                current.GetProperty("surface").GetString()!),
            example.GetProperty("revision").GetInt64(),
            null,
            example.GetProperty("room").Clone());

        var json = Serialise(state);

        JsonShape.AssertMatches(json, example, "audience-events.json audienceState", ScreenShapes.AudienceState);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void AudienceState_WhenNotLive_ShouldSendNoSlideAndNeverTheDeckId()
    {
        var json = Serialise(new AudienceState("7Kq2xYp9LmQa", 0, AudiencePhase.Lobby, null, 7, null, null));

        JsonShape.AssertMatches(json, Example().GetProperty("audienceState"), "audience-events.json audienceState", ScreenShapes.AudienceState);
        json.GetRawText().ShouldContain("\"phase\":\"lobby\"", Case.Sensitive);
        json.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        json.TryGetProperty("deckId", out _).ShouldBeFalse();
    }

    [Test]
    public void AudienceEnded_ShouldMatchContract()
    {
        var example = Example().GetProperty("audienceEnded");

        var json = Serialise(new AudienceEnded("7Kq2xYp9LmQa"));

        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void JoinedCount_ShouldMatchContract()
    {
        var example = Example().GetProperty("joinedCount");

        var json = Serialise(new JoinedCount("7Kq2xYp9LmQa", example.GetProperty("count").GetInt32()));

        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    private static JsonElement Serialise(AudienceEvent audienceEvent) => JsonSerializer.SerializeToElement(audienceEvent, JsonSerializerOptions.Web);

    private static JsonElement Example() =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "audience-events.json")));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
