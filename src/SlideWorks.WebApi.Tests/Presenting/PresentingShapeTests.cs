using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// SW-4's contract samples (P1, P3, P4) against their records. Each sample is read into its record with the app's JSON
// options and written back, and the JSON must equal the sample exactly: names, nesting, value kinds and the camelCase
// enum values. JsonShape then checks the nullable paths that the endpoints' and the hub's tests use on real responses.
// The hub's own JSON protocol is checked by T0.1a's hub-level round trip, not here.
[TestFixture]
public class PresentingShapeTests : ApiTestFixture
{
    [Test]
    public void PresentationState_WhenReadAndWritten_ShouldMatchTheContractSample()
    {
        var example = Read(ScreenShapes.PresentationStateFile);

        var state = example.Deserialize<PresentationState>(AppJsonOptions())!;
        var json = Serialise(state);

        JsonShape.AssertMatches(json, ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        state.Timer.ShouldBe(new PresentationTimer(TimerState.Running, 760000));
        state.Connected.Select(c => c.Role).ShouldBe([PresentationRole.Presenter, PresentationRole.Audience, PresentationRole.Phone]);
    }

    [Test]
    public void PresentationState_WhenThereIsNoTalkLength_ShouldMatchTheContract()
    {
        var state = new PresentationState("7Kq2abcdEFGH", "p-3f9c2a", 1, 1, 0, false, false, new PresentationTimer(TimerState.NotStarted, 0), null, []);

        var json = Serialise(state);

        JsonShape.AssertMatches(json, ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState);
        json.GetProperty("talkMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
    }

    [TestCase(TimerState.NotStarted, "notStarted")]
    [TestCase(TimerState.Running, "running")]
    [TestCase(TimerState.Paused, "paused")]
    public void TimerState_WhenWritten_ShouldBeCamelCase(TimerState state, string wire)
    {
        Serialise(state).GetString().ShouldBe(wire);
    }

    [TestCase(TimerCommand.Pause, "pause")]
    [TestCase(TimerCommand.Resume, "resume")]
    [TestCase(TimerCommand.Reset, "reset")]
    public void TimerCommand_WhenWritten_ShouldBeCamelCase(TimerCommand command, string wire)
    {
        Serialise(command).GetString().ShouldBe(wire);
    }

    [TestCase(PresentationRole.Presenter, "presenter")]
    [TestCase(PresentationRole.Audience, "audience")]
    [TestCase(PresentationRole.Phone, "phone")]
    public void PresentationRole_WhenWritten_ShouldBeCamelCase(PresentationRole role, string wire)
    {
        Serialise(role).GetString().ShouldBe(wire);
    }

    // An enum is never read from a number, as SW-1's enums aren't.
    [TestCase("1")]
    [TestCase("\"1\"")]
    public void PresentationTimer_WhenStateIsNotAWireName_ShouldBeRefused(string state)
    {
        var body = $$"""{ "state": {{state}}, "elapsedMs": 0 }""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<PresentationTimer>(body, AppJsonOptions()));
    }

    [Test]
    public void PresentationJoined_WhenWritten_ShouldCarryTheStateAndClockMinutes()
    {
        var state = Read(ScreenShapes.PresentationStateFile).Deserialize<PresentationState>(AppJsonOptions())!;

        var json = Serialise(new PresentationJoined(state, 760));

        json.EnumerateObject().Select(p => p.Name).ShouldBe(["state", "clockMinutes"]);
        JsonShape.AssertMatches(json.GetProperty("state"), ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState);
        json.GetProperty("clockMinutes").GetInt32().ShouldBe(760);
    }

    [Test]
    public void PresentationResume_WhenRead_ShouldTakeThePositionSwitchesAndTimer()
    {
        const string BODY = """{ "slide": 9, "step": 2, "black": true, "skipDeepDives": true, "timer": { "state": "paused", "elapsedMs": 61000 } }""";

        var resume = JsonSerializer.Deserialize<PresentationResume>(BODY, AppJsonOptions())!;

        resume.ShouldBe(new PresentationResume(9, 2, true, true, new PresentationTimer(TimerState.Paused, 61000)));
        SameJson(Serialise(resume), JsonSerializer.Deserialize<JsonElement>(BODY)).ShouldBeTrue();
    }

    [Test]
    public void PresentationUpdate_WhenRead_ShouldTakeThePositionAndSwitches()
    {
        const string BODY = """{ "slide": 11, "step": 0, "black": false, "skipDeepDives": true }""";

        var update = JsonSerializer.Deserialize<PresentationUpdate>(BODY, AppJsonOptions())!;

        update.ShouldBe(new PresentationUpdate(11, 0, false, true));
        SameJson(Serialise(update), JsonSerializer.Deserialize<JsonElement>(BODY)).ShouldBeTrue();
    }

    [Test]
    public void Presenter_WhenReadAndWritten_ShouldMatchTheContractSample()
    {
        var example = Read(Path.Combine(JsonShape.ScreensDirectory(), "presenter.json"));

        var view = example.Deserialize<PresenterView>(AppJsonOptions())!;
        var json = Serialise(view);

        JsonShape.AssertMatches(json, "presenter.json", ScreenShapes.Presenter);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        view.Slides.Select(s => s.Number).ShouldBe(Enumerable.Range(1, 16));
        view.Slides.Where(s => s.DeepDive).Select(s => s.Number).ShouldBe([5, 10, 12]);
        view.Slides.ShouldAllBe(s => s.Notes != null);
        view.Slides.Where(s => s.Notes!.Contains("**", StringComparison.Ordinal)).Select(s => s.Number).ShouldBe([1, 8, 10, 12]);
        view.Slides.ShouldAllBe(s => s.Headline != null);
        view.Author.ShouldBe("Sam Rivera");
        view.TalkMinutes.ShouldBe(30);
        view.AudienceUrl.ShouldBe("https://slideworks.example/d/7Kq2abcdEFGH/ship-smaller?present");
        view.RemoteUrl.ShouldBe("https://slideworks.example/remote?deck=7Kq2abcdEFGH");
    }

    // The sample deck's slots are the source of truth (K3): the presenter sample carries the same slides.
    [Test]
    public void Presenter_Slides_ShouldCarryTheSampleDecksSlots()
    {
        var presenter = Read(Path.Combine(JsonShape.ScreensDirectory(), "presenter.json")).GetProperty("slides");
        var sample = Read(Path.Combine(JsonShape.ScreensDirectory(), "..", "sample-deck.json")).GetProperty("slides");

        presenter.GetArrayLength().ShouldBe(sample.GetArrayLength());
        presenter.EnumerateArray().Zip(sample.EnumerateArray())
            .Where(pair => !SameJson(pair.First.GetProperty("slots"), pair.Second.GetProperty("slots"))
                || pair.First.GetProperty("layout").GetString() != pair.Second.GetProperty("layout").GetString()
                || pair.First.GetProperty("steps").GetInt32() != pair.Second.GetProperty("steps").GetInt32())
            .Select(pair => pair.First.GetProperty("number").GetInt32())
            .ShouldBeEmpty();
    }

    [Test]
    public void Presenter_WhenOptionalFieldsAreNull_ShouldMatchTheContract()
    {
        var example = Read(Path.Combine(JsonShape.ScreensDirectory(), "presenter.json")).Deserialize<PresenterView>(AppJsonOptions())!;
        var slide = example.Slides[0] with { Part = null, Notes = null, Headline = null };

        var json = Serialise(example with { Author = null, TalkMinutes = null, Slides = [slide] });

        JsonShape.AssertMatches(json, "presenter.json", ScreenShapes.Presenter);
        json.GetProperty("talkMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("slides")[0].GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public void Presentations_WhenReadAndWritten_ShouldMatchTheContractSample()
    {
        var example = Read(Path.Combine(JsonShape.ScreensDirectory(), "presentations.json"));

        var view = example.Deserialize<LivePresentationsView>(AppJsonOptions())!;
        var json = Serialise(view);

        JsonShape.AssertMatches(json, "presentations.json", ScreenShapes.Presentations);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        view.Presentations.ShouldHaveSingleItem().Windows.ShouldBe([new LiveWindow(PresentationRole.Presenter, "Chrome on Windows")]);
    }

    // The three samples describe one talk: the same deck, live on the same slide.
    [Test]
    public void Samples_ShouldDescribeTheSameDeck()
    {
        var presenter = Read(Path.Combine(JsonShape.ScreensDirectory(), "presenter.json")).Deserialize<PresenterView>(AppJsonOptions())!;
        var state = Read(ScreenShapes.PresentationStateFile).Deserialize<PresentationState>(AppJsonOptions())!;
        var live = Read(Path.Combine(JsonShape.ScreensDirectory(), "presentations.json")).Deserialize<LivePresentationsView>(AppJsonOptions())!.Presentations[0];

        state.DeckId.ShouldBe(presenter.Id);
        state.TalkMinutes.ShouldBe(presenter.TalkMinutes);
        live.DeckId.ShouldBe(presenter.Id);
        live.Title.ShouldBe(presenter.Title);
        live.Total.ShouldBe(presenter.Slides.Count);
        live.Slide.ShouldBe(state.Slide);
    }

    private JsonSerializerOptions AppJsonOptions() =>
        Factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

    private JsonElement Serialise<T>(T value) => JsonSerializer.SerializeToElement(value, AppJsonOptions());

    private static JsonElement Read(string path) => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
