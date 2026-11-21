using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-3's visual check with the real render engine and the built bundle. Slide 2 of the overflow deck is the shared
// levelsTextWide fixture: add_slide accepts it (the estimate is blind to wide letters) but it overflows when measured.
public class Sw3CheckJourneyTests : AcceptanceTest
{
    private const string OVERFLOW_MESSAGE = "Text runs below the footer";

    [Test]
    public async Task CheckDeck_WhenAWideLetterSlideIsAccepted_ShouldReportOverflow()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var (deck, warnings) = await OverflowDeckAsync(agent);
        warnings.GetArrayLength().ShouldBe(0);

        var result = await AgentSeed.CallAsync(agent, "check_deck", new() { ["deck"] = deck });

        result.GetProperty("measured").GetBoolean().ShouldBeTrue();
        result.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
        var overflow = FindingsOf(result, 2).Where(f => f.GetProperty("rule").GetString() == "overflow").ToList();
        overflow.ShouldNotBeEmpty();
        overflow.ShouldAllBe(f => f.GetProperty("severity").GetString() == "error");
        FindingsOf(result, 1).ShouldBeEmpty();
        FindingsOf(result, 3).ShouldBeEmpty();
    }

    [Test]
    public async Task PreviewDeck_WhenASlideOverflows_ShouldReturnTheFindingWithThePng()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var (deck, _) = await OverflowDeckAsync(agent);

        var result = await agent.CallToolAsync("preview_deck", new Dictionary<string, object?> { ["deck"] = deck, ["slides"] = new[] { 2 } });

        (result.IsError ?? false).ShouldBeFalse();
        var preview = JsonSerializer.Deserialize<JsonElement>(result.Content.OfType<TextContentBlock>().Single().Text);
        var png = result.Content.OfType<ImageContentBlock>().Single().DecodedData.ToArray();
        AgentSeed.PngSize(png).ShouldBe((1920, 1080));
        FindingsOf(preview.GetProperty("findings"), 2).ShouldContain(f => f.GetProperty("rule").GetString() == "overflow");
    }

    [Test]
    public async Task CheckDeck_WhenTheDeckIsClean_ShouldReturnNoErrorsOrWarnings()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CleanDeckAsync(agent);

        var result = await AgentSeed.CallAsync(agent, "check_deck", new() { ["deck"] = deck });

        result.GetProperty("measured").GetBoolean().ShouldBeTrue();
        result.GetProperty("findings").EnumerateArray()
            .Where(f => f.GetProperty("severity").GetString() != "note").ShouldBeEmpty();
    }

    [Test]
    public async Task DeckPage_WhenASlideOverflows_ShouldShowTheBadgeAndTheList()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var (deck, _) = await OverflowDeckAsync(agent);
        var checks = await AgentSeed.CallAsync(agent, "check_deck", new() { ["deck"] = deck });
        var problems = FindingsOf(checks, 2).Count(f => f.GetProperty("severity").GetString() != "note");

        await SignIn.AsAsync(Page, TestUser.Sam);
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await deckPage.SelectSlideAsync(2);

        await Expect(deckPage.ChecksBadge(2)).ToHaveAttributeAsync("aria-label", new Regex($"^Slide 2: {problems} problems?$"));
        await Expect(deckPage.ChecksRows().Filter(new() { HasText = OVERFLOW_MESSAGE }).First).ToBeVisibleAsync();
        // The measure writes every slide at once; wait for its marker so "no badge" means "measured clean".
        await Expect(deckPage.ChecksMeasured()).ToHaveAttributeAsync("data-checks-measured", "true");
        await Expect(deckPage.ChecksBadge(1)).ToHaveCountAsync(0);
        await Expect(deckPage.ChecksBadge(3)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task DeckPage_WhenTheDeckIsClean_ShouldShowNoBadges()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CleanDeckAsync(agent);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await deckPage.SelectSlideAsync(2);

        await Expect(deckPage.ChecksMeasured()).ToHaveAttributeAsync("data-checks-measured", "true");
        await Expect(deckPage.ChecksList()).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("slide-checks-none")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("slide-checks-badge")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task CheckDeck_WhenASlideFailsToRender_ShouldReportRenderFailedAndCheckTheRest()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await NewDeckAsync(agent);
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Ship small, ship often");
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "broken.png", ["contentType"] = "image/png" });
        byte[] broken = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];
        using (var http = new HttpClient())
        using (var content = new ByteArrayContent(broken))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var put = await http.PutAsync(upload.GetProperty("uploadUrl").GetString(), content);
            put.StatusCode.ShouldBe(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        }

        await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "image",
            ["slots"] = JsonSerializer.SerializeToElement(new
            {
                kicker = "Picture",
                headline = "A picture that will not decode",
                image = new { image = upload.GetProperty("image").GetString(), alt = "A team around a whiteboard" },
                points = new[] { "One point", "Another point" },
                caption = "Caption",
            }),
        });

        var result = await AgentSeed.CallAsync(agent, "check_deck", new() { ["deck"] = deck });

        result.GetProperty("measured").GetBoolean().ShouldBeTrue();
        var failed = FindingsOf(result, 2).Single(f => f.GetProperty("rule").GetString() == "renderFailed");
        failed.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain(upload.GetProperty("image").GetString()!);
        FindingsOf(result, 1).ShouldBeEmpty();
    }

    private static List<JsonElement> FindingsOf(JsonElement result, int slide)
    {
        var findings = result.ValueKind == JsonValueKind.Array ? result : result.GetProperty("findings");
        return [.. findings.EnumerateArray().Where(f => f.GetProperty("slide").GetInt32() == slide)];
    }

    // src/contracts/fit-fixtures/levels.json, found by walking up from the test binaries to the folder with the solution.
    private static string FixturePath()
    {
        var start = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException($"SlideWorks.slnx was not found in or above {start}");
        }

        return Path.Combine(directory.FullName, "contracts", "fit-fixtures", "levels.json");
    }

    private static async Task<string> NewDeckAsync(McpClient agent)
    {
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Checks {Guid.NewGuid():N}" });
        return created.GetProperty("deck").GetString()!;
    }

    private static async Task<string> CleanDeckAsync(McpClient agent)
    {
        var deck = await NewDeckAsync(agent);
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Ship small, ship often", "Measure twice, cut once", "Make it work, then make it fast");
        return deck;
    }

    // Slide 1 and 3 are clean quotes; slide 2 is levelsTextWide, which add_slide accepts without a warning (SW-664).
    private static async Task<(string Deck, JsonElement Warnings)> OverflowDeckAsync(McpClient agent)
    {
        var deck = await NewDeckAsync(agent);
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Ship small, ship often");
        using var fixtures = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath()));
        var slots = fixtures.RootElement.GetProperty("levelsTextWide").GetProperty("slots").Clone();
        var added = await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = "levels", ["slots"] = slots });
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Measure twice, cut once");
        return (deck, added.GetProperty("warnings").Clone());
    }
}
