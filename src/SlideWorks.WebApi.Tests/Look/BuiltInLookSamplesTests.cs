using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// The six deck samples in src/contracts/screens show a built-in deck exactly as the server sends it (T7): they are the
// client's fixtures, so a made-up logo url or version in one would be drawn by every fixture deck. Each sample's look is
// held against the play view of a real deck on the sample's template: the same members null, the same art kind, the
// same empty lists. template-public.json is the sample of an uploaded template, and is not checked here.
[TestFixture]
public class BuiltInLookSamplesTests : ApiTestFixture
{
    private static readonly string[] _emptyForABuiltIn = ["fonts", "extras", "customLayouts"];

    [TestCase("deck-play.json", "look")]
    [TestCase("deck-page.json", "look")]
    [TestCase("presenter.json", "look")]
    [TestCase("card.json", "look")]
    [TestCase("look-change.json", "response.look")]
    [TestCase("guest.json", "live.look")]
    [TestCase("guest.json", "lobby.look")]
    [TestCase("guest.json", "ended.look")]
    public async Task Play_WhenTheDeckIsOnABuiltInTemplate_ShouldBeNullWhereTheSamplesLookIs(string file, string lookPath)
    {
        var sample = lookPath.Split('.').Aggregate(
            JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(JsonShape.ScreensDirectory(), file))),
            (element, name) => element.GetProperty(name));
        var created = await Client.PostAsJsonAsync("/api/decks", new { title = "Built-in look", template = sample.GetProperty("templateRef").GetString() });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var deck = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var real = (await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}")).GetProperty("look");

        WhatTheTemplateBrings(sample).ShouldBe(WhatTheTemplateBrings(real), $"{file} {lookPath}");
        ScreenShapes.LookNullablePaths.Where(path => At(real, path).ValueKind != JsonValueKind.Null).ShouldBeEmpty();
    }

    // The part of a look that comes from the template's files: which nullable members are null, and every value a
    // built-in sends the same for every deck.
    private static string WhatTheTemplateBrings(JsonElement look)
    {
        var nulls = ScreenShapes.LookNullablePaths.Select(path => $"{path}: {(At(look, path).ValueKind == JsonValueKind.Null ? "null" : "set")}");
        var lists = _emptyForABuiltIn.Select(name => $"{name}: {look.GetProperty(name).GetArrayLength()}");
        string[] values =
        [
            $"art.kind: {look.GetProperty("art").GetProperty("kind").GetString()}",
            $"logos.showOnDark: {look.GetProperty("logos").GetProperty("showOnDark").GetBoolean()}",
            $"ornament.colours: {look.GetProperty("ornament").GetProperty("colours").GetArrayLength()}",
            $"topBar: {look.GetProperty("topBar").GetString()}",
            $"templateMissing: {look.GetProperty("templateMissing").GetBoolean()}",
        ];
        return string.Join("; ", [.. nulls, .. lists, .. values]);
    }

    // The member of a look at one of ScreenShapes.LookNullablePaths, which are written from the screen ("look.art.url").
    private static JsonElement At(JsonElement look, string path)
        => path.Split('.').Skip(1).Aggregate(look, (element, name) => element.GetProperty(name));
}
