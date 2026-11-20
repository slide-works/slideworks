using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// The L6 look records against their JSON examples in src/contracts/screens, serialised with the app's JSON options.
[TestFixture]
public class LookRecordShapeTests : ApiTestFixture
{
    [Test]
    public void DeckPlay_ShouldMatchContract()
    {
        var example = Example("deck-play.json");
        var deck = new Deck
        {
            Id = example.GetProperty("id").GetString()!,
            Slug = example.GetProperty("slug").GetString()!,
            Title = example.GetProperty("title").GetString()!,
            Author = example.GetProperty("author").GetString(),
            PartsJson = example.GetProperty("parts").GetRawText(),
            Template = "workshop",
            Palette = "cobalt",
        };
        var slides = example.GetProperty("slides").EnumerateArray().Select((slide, position) => new Slide
        {
            DeckId = deck.Id,
            Position = position,
            Layout = slide.GetProperty("layout").GetString()!,
            Part = slide.GetProperty("part").ValueKind == JsonValueKind.Null ? null : slide.GetProperty("part").GetInt32(),
            SlotsJson = slide.GetProperty("slots").GetRawText(),
            Steps = slide.GetProperty("steps").GetInt32(),
            DeepDive = slide.GetProperty("deepDive").GetBoolean(),
        });

        var json = Serialise(DeckPlayView.From(deck, slides, BuiltInTemplates.Resolve(new TemplateCatalogue().Find("workshop")!), example.GetProperty("look").Deserialize<LookView>(AppJsonOptions())!, example.GetProperty("join").Deserialize<JoinView>(AppJsonOptions())!, example.GetProperty("isOwner").GetBoolean()));

        JsonShape.AssertMatches(json, "deck-play.json", ScreenShapes.DeckPlay);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void LookView_WhenBuiltFromADeck_ShouldMatchTheExamplesShape()
    {
        var deck = new Deck { Id = "7Kq2xYp9LmQa", Slug = "s", Title = "T", PartsJson = "[]", Template = "update", Palette = "midnight" };

        var json = Serialise(LookViews.From(deck, BuiltIn("update"), Palettes));

        Should.NotThrow(() => JsonShape.AssertMatches(json, Example("card.json").GetProperty("look"), "card.json look", LookAlone));
    }

    // T7: a built-in brings no files, so every new field has the value that draws today's look.
    [Test]
    public void LookView_WhenTheTemplateIsBuiltIn_ShouldCarryNothingOfItsOwn()
    {
        var deck = new Deck { Id = "7Kq2xYp9LmQa", Slug = "s", Title = "T", PartsJson = "[]", Template = "update", Palette = "midnight" };

        var look = LookViews.From(deck, BuiltIn("update"), Palettes);

        look.Fonts.ShouldBeEmpty();
        look.Logos.ShouldBe(new LookLogosView(null, null, false));
        look.Art.ShouldBe(new LookArtView("none", null, null));
        look.Ornament.Colours.ShouldBeEmpty();
        look.TopBar.ShouldBe("none");
        look.TemplateRef.ShouldBe("update");
        look.TemplateVersion.ShouldBeNull();
        look.LatestVersion.ShouldBeNull();
        look.Extras.ShouldBeEmpty();
        look.CustomLayouts.ShouldBeEmpty();
        look.TemplateMissing.ShouldBeFalse();
    }

    // D13: a deck that never set the top bar stores, and sends, the chrome overrides it always did.
    [Test]
    public void ChromeOverrides_WhenTheTopBarIsNotSet_ShouldLeaveItOutOfTheJson()
    {
        var stored = new ChromeOverridesView(false, null, null, true).ToJson();

        stored.ShouldBe("""{"tracker":false,"footer":null,"progress":null,"joinBar":true}""");
        ChromeOverridesView.From(stored).TopBar.ShouldBeNull();
        (ChromeOverridesView.None with { TopBar = "accent" }).ToJson().ShouldBe("""{"tracker":null,"footer":null,"progress":null,"joinBar":false,"topBar":"accent"}""");
        ChromeOverridesView.None.ToJson().ShouldBeNull();
    }

    [Test]
    public void Looks_ShouldMatchContract()
    {
        var json = Serialise(LooksView.From([.. new TemplateCatalogue().All.Select(BuiltInTemplates.Resolve)], Palettes, callerId: null));

        Should.NotThrow(() => JsonShape.AssertMatches(json, "looks.json", ScreenShapes.Looks));
    }

    [Test]
    public void Card_ShouldMatchContract()
    {
        var example = Example("card.json");
        var view = CardView.ForDeck(
            example.GetProperty("title").GetString()!,
            example.GetProperty("subtitle").GetString(),
            example.GetProperty("author").GetString(),
            example.GetProperty("slideCount").GetInt32(),
            example.GetProperty("look").Deserialize<LookView>(AppJsonOptions())!);

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "card.json", ScreenShapes.Card);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void Card_WhenGeneric_ShouldMatchContractWithNoDeckDetails()
    {
        var json = Serialise(CardView.GenericWith(LookViews.Default(BuiltInTemplates.Resolve(new TemplateCatalogue().Default), Palettes, Clock.GetUtcNow())));

        JsonShape.AssertMatches(json, "card.json", ScreenShapes.Card);
        json.GetProperty("generic").GetBoolean().ShouldBeTrue();
        json.GetProperty("title").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A look on its own, outside a screen: the same nullable paths without the "look." prefix.
    private static JsonShapeOptions LookAlone => new()
    {
        NullablePaths = ["template.titleArt", "logo", .. ScreenShapes.LookNullablePaths.Select(path => path["look.".Length..])],
    };

    private static ResolvedTemplate BuiltIn(string name) => BuiltInTemplates.Resolve(new TemplateCatalogue().Find(name)!);

    private IPaletteCatalogue Palettes => Factory.Services.GetRequiredService<IPaletteCatalogue>();

    private JsonSerializerOptions AppJsonOptions() =>
        Factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

    private JsonElement Serialise<T>(T record) => JsonSerializer.SerializeToElement(record, AppJsonOptions());

    private static JsonElement Example(string file) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
