using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// GET /api/play/{id} (K4, deck-play.json): the real response matches the contract example, slots are checked through the
// validator rather than the shape (they vary by layout), and notes never reach a viewer (D16).
[TestFixture]
public class PlayViewTests : ApiTestFixture
{
    private static readonly JsonShapeOptions PLAY_SHAPE = new()
    {
        NullablePaths = ["author", "slides[].part"],
        OpaquePaths = ["slides[].slots"],
    };

    // red until SW-63: deck-play.json and deck-page.json gained look, revision and deepDive (SW-2 L6)
    [Test]
    public async Task Get_WhenOwner_ShouldMatchShapeAndOmitNotes()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Ship smaller", DeckSeed.SAMPLE_PARTS, "Sam Rivera");
        var sample = DeckSeed.SampleSlides();
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.SamplePng());
        foreach (var slide in sample)
        {
            var part = slide.GetProperty("part");
            await DeckSeed.AddSlideAsync(
                agent,
                deck,
                slide.GetProperty("layout").GetString()!,
                slide.GetProperty("slots").GetRawText().Replace(DeckSeed.SAMPLE_IMAGE_ID, image, StringComparison.Ordinal),
                part.ValueKind == JsonValueKind.Null ? null : part.GetInt32(),
                notes: "SECRET-SPEAKER-NOTE");
        }

        var response = await Client.GetAsync($"/api/play/{deck}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("SECRET-SPEAKER-NOTE");
        body.ShouldNotContain("\"notes\"");
        var play = JsonDocument.Parse(body).RootElement;
        JsonShape.AssertMatches(play, "deck-play.json", PLAY_SHAPE);
        play.GetProperty("id").GetString().ShouldBe(deck);
        play.GetProperty("slug").GetString().ShouldBe("ship-smaller");
        play.GetProperty("author").GetString().ShouldBe("Sam Rivera");
        var slides = play.GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("number").GetInt32()).ShouldBe(Enumerable.Range(1, sample.Count));
        slides.Select(s => s.GetProperty("layout").GetString()).ShouldBe(sample.Select(s => s.GetProperty("layout").GetString()));
        slides.Select(s => s.GetProperty("steps").GetInt32()).ShouldBe(sample.Select(s => s.GetProperty("steps").GetInt32()));
        foreach (var slide in slides)
        {
            SlideValidator.Validate(slide.GetProperty("layout").GetString()!, slide.GetProperty("slots")).Errors.ShouldBeEmpty();
        }
        slides.SelectMany(s => SlideValidator.Validate(s.GetProperty("layout").GetString()!, s.GetProperty("slots")).ImageIds).ShouldBe([image]);
        (await Client.GetAsync($"/api/images/{image}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // red until SW-63: deck-play.json and deck-page.json gained look, revision and deepDive (SW-2 L6)
    [Test]
    public async Task Get_WhenNoAuthorAndNoSlides_ShouldStillMatchShape()
    {
        var created = await Client.PostAsJsonAsync("/api/decks", new { title = "Empty" });
        var deck = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        play.GetProperty("author").ValueKind.ShouldBe(JsonValueKind.Null);
        play.GetProperty("slides").GetArrayLength().ShouldBe(0);
        JsonShape.AssertMatches(play, "deck-play.json", PLAY_SHAPE);
    }

    [Test]
    public async Task Get_WhenAnonymousAndPrivate_ShouldReturn404()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var anonymous = ClientFor(TestUser.Anonymous);
        using var alex = ClientFor(TestUser.Alex);

        var anonymousResponse = await anonymous.GetAsync($"/api/play/{deck}");
        var alexResponse = await alex.GetAsync($"/api/play/{deck}");

        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymousResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        alexResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await alexResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
    }

    [Test]
    public async Task Get_WhenUnknown_ShouldReturn404()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var signedIn = await Client.GetAsync("/api/play/nOsUcHdEcK00");
        var signedOut = await anonymous.GetAsync("/api/play/nOsUcHdEcK00");

        signedIn.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await signedIn.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        signedOut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await signedOut.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
    }
}
