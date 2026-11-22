using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50: a template with its own font is archived, and a deck on it still draws in that font for its owner and for an
// anonymous viewer, while nobody can pick the template for a new deck. Sam builds the template and the deck over MCP;
// only a person confirms font rights (through the route the editor uses), and only the owner can archive.
public class Sw50ArchiveJourneyTests : AcceptanceTest
{
    private const string FAMILY = "Archive Sans";
    private const string HEADING = "Small batches ship sooner";
    private const string FRAMES = "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))";
    private const string NEW_DECK_TEMPLATES = """
        el => [...el.querySelectorAll('[data-testid^="new-deck-template-"]')].map(o => o.dataset.testid.slice('new-deck-template-'.length))
        """;
    private const string HEADING_FONT = "el => getComputedStyle(el).fontFamily.replace(/[\"']/g, '')";
    private const string FACE_LOADED = "family => document.fonts.check(`16px \"${family}\"`)";
    private const string SELECTED_LABEL = "select => select.selectedOptions[0].textContent";

    [Test]
    public async Task Owner_WhenArchiving_ShouldKeepDecksRendering()
    {
        var name = $"Archive Studio {Guid.NewGuid():N}"[..28];
        await SignIn.AsAsync(Page, TestUser.Sam);
        using var oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");
        await using var agent = await AgentSession.ConnectAsync(oauth, Page, TestUser.Sam);

        var created = await agent.CallAsync("create_template", new { name, from = "talk" });
        var id = created.GetProperty("template").GetString().ShouldNotBeNull();

        var upload = await agent.CallAsync("upload_asset", new { template = id, kind = "font", filename = "archive-sans.woff2" });
        upload.GetProperty("kind").GetString().ShouldBe("font");
        var assetId = await PutFontAsync(upload.GetProperty("upload_url").GetString().ShouldNotBeNull(), FontFixtures.Normal());

        var manifest = (await agent.CallAsync("get_template", new { template = id })).GetProperty("manifest");
        var type = JsonNode.Parse(manifest.GetProperty("type").GetRawText()).ShouldNotBeNull().AsObject();
        type["head"] = FAMILY;
        await agent.CallAsync("update_template", new
        {
            template = id,
            manifest = new { type, fonts = new[] { new { role = "head", family = FAMILY, file = assetId, weight = 400, style = "normal" } } },
        });

        var rights = await Page.APIRequest.PostAsync($"/api/templates/{id}/assets/font-rights", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        rights.Status.ShouldBe(200, await rights.TextAsync());
        JsonDocument.Parse(await rights.TextAsync()).RootElement.GetProperty("confirmed").GetInt32().ShouldBe(1);

        var published = await agent.CallAsync("publish_template", new { template = id });
        published.GetProperty("version").GetInt32().ShouldBe(1);

        var created2 = await agent.CallAsync("create_deck", new { title = $"On {FAMILY} {Guid.NewGuid():N}"[..28], template = id });
        var deck = created2.GetProperty("deck").GetString().ShouldNotBeNull();
        await agent.CallAsync("add_slide", new { deck, layout = "title", slots = new { title = HEADING } });
        await agent.CallAsync("set_sharing", new { deck, level = "link" });
        var shared = await agent.CallAsync("get_deck", new { deck });
        shared.GetProperty("sharing").GetString().ShouldBe("link");
        var link = shared.GetProperty("url").GetString().ShouldNotBeNull();

        // 1. Alex cannot archive it (404, nothing changes); Sam's dialog still lists it (the companion to the absence below).
        await using var alexContext = await Browser.NewContextAsync(ContextOptions());
        var alexPage = await alexContext.NewPageAsync();
        await SignIn.AsAsync(alexPage, TestUser.Alex);
        var refused = await ArchiveAsync(alexPage, id);
        refused.Status.ShouldBe(404, await refused.TextAsync());

        var decks = new DecksPage(Page);
        await decks.GotoAsync();
        var before = await decks.OpenNewDeckAsync();
        await Expect(before.Template("talk")).ToBeVisibleAsync();
        await Expect(before.Template(id)).ToBeVisibleAsync();
        await Page.EvaluateAsync(FRAMES);
        (await before.Root.EvaluateAsync<string[]>(NEW_DECK_TEMPLATES)).ShouldContain(id);

        // 2. Sam archives it; the dialog no longer offers it, though the same probe found it before.
        await before.Cancel.ClickAsync();
        var archived = await ArchiveAsync(Page, id);
        archived.Status.ShouldBe(204, await archived.TextAsync());
        await decks.GotoAsync();
        var after = await decks.OpenNewDeckAsync();
        await Expect(after.Template("talk")).ToBeVisibleAsync();
        await Page.EvaluateAsync(FRAMES);
        var offered = await after.Root.EvaluateAsync<string[]>(NEW_DECK_TEMPLATES);
        offered.ShouldContain("talk");
        offered.ShouldNotContain(id);

        // 3. Sam's player still draws the deck in the template's font.
        var player = new PlayerPage(Page);
        await player.GotoAsync(deck);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("title-title")).ToHaveTextAsync(HEADING);
        await Expect(player.Error).ToHaveCountAsync(0);
        await player.FontsReadyAsync();
        await AssertDrawnInFontAsync(Page);

        // 4. An anonymous viewer on the share link: the archived template's file is still served, and the heading uses it.
        await using var anonymous = await Browser.NewContextAsync(ContextOptions());
        var guest = await anonymous.NewPageAsync();
        var guestPlayer = new PlayerPage(guest);
        var fontServed = await guest.RunAndWaitForResponseAsync(
            async () => await guest.GotoAsync(link), r => r.Url.Contains($"/api/templates/{id}/assets/{assetId}"));
        fontServed.Status.ShouldBe(200);
        await Expect(guestPlayer.Slide(1)).ToBeVisibleAsync();
        await Expect(guest.GetByTestId("title-title")).ToHaveTextAsync(HEADING);
        await Expect(guestPlayer.Error).ToHaveCountAsync(0);
        await guestPlayer.FontsReadyAsync();
        await AssertDrawnInFontAsync(guest);

        // 5. The deck page's Look panel still names the template.
        await Page.GotoAsync(DeckPage.PathFor(deck));
        var look = new DeckPage(Page).Look;
        await Expect(look.Root).ToBeVisibleAsync();
        await Expect(look.Template).ToBeEnabledAsync();
        (await look.Template.EvaluateAsync<string>(SELECTED_LABEL)).ShouldBe(name);

        // 6. get_deck over MCP still pins the template at version 1.
        var read = (await agent.CallAsync("get_deck", new { deck })).GetProperty("look");
        read.GetProperty("template").GetString().ShouldBe(id);
        read.GetProperty("templateVersion").GetInt32().ShouldBe(1);
    }

    // The heading is drawn in the template's face: its computed family starts with it, and the face is loaded.
    private static async Task AssertDrawnInFontAsync(IPage page)
    {
        var family = await page.GetByTestId("title-title").EvaluateAsync<string>(HEADING_FONT);
        family.ShouldStartWith(FAMILY);
        var loaded = await Eventually.ReadAsync(() => page.EvaluateAsync<bool>(FACE_LOADED, FAMILY), ok => ok);
        loaded.ShouldBeTrue();
    }

    private static Task<IAPIResponse> ArchiveAsync(IPage page, string id) =>
        page.APIRequest.PostAsync($"/api/templates/{id}/archive", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });

    // The agent PUTs the font's bytes to the one-time upload URL, with no auth header; the answer is JSON with the asset's id.
    private static async Task<string> PutFontAsync(string uploadUrl, byte[] font)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(font);
        content.Headers.ContentType = new MediaTypeHeaderValue("font/woff2");
        using var response = await http.PutAsync(uploadUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("assetId").GetString().ShouldNotBeNull();
    }
}
