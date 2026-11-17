using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.AcceptanceTests.Journeys.TenSlideBrief;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-1's Measure (D21): an agent builds a ten-slide deck from a brief through MCP alone, with no hand-written HTML, and
// the deck presents correctly at 1920×1080 and on a 390 px phone. The agent finds the OAuth server from /mcp's 401,
// registers itself, gets Sam's consent in the browser and exchanges the code with PKCE; then it builds the deck with the
// tools an agent has (layouts, an uploaded image, an insert, a move, a delete, an update, notes, sharing and the link).
// Sam presents it in the browser, and an anonymous guest opens the shared link on a phone.
public class MeasureJourney : AcceptanceTest
{
    private const int SLIDES = 10;

    [Test]
    public async Task AgentBuildsTenSlideDeckOverMcpAndItPresents_WhenAgentHasOnlyTheMcpUrl_ShouldPresentAt1920AndOnAPhone()
    {
        using var oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");
        await using var agent = await AgentSession.ConnectAsync(oauth, Page, TestUser.Sam);

        var (deckId, playUrl, deepLink) = await BuildDeckAsync(agent);

        await PresentAt1920Async(deckId, playUrl, deepLink);
        await PresentOnPhoneAsGuestAsync(playUrl);
    }

    // What the agent does, tool by tool. Returns the deck's id, its share link and a link to the bullets slide's second step.
    private static async Task<(string DeckId, string PlayUrl, string DeepLink)> BuildDeckAsync(AgentSession agent)
    {
        var layouts = (await agent.CallAsync("list_layouts", new { })).GetProperty("layouts").EnumerateArray()
            .Select(l => l.GetProperty("name").GetString()).ToList();
        layouts.Count.ShouldBe(19);
        LAYOUTS.Cast<string?>().ShouldBeSubsetOf(layouts);

        var created = await agent.CallAsync("create_deck", new { title = TITLE, subtitle = "Small batches, sooner feedback", author = "Sam Rivera", parts = PARTS });
        created.GetProperty("sharing").GetString().ShouldBe("private");
        var deck = created.GetProperty("deck").GetString().ShouldNotBeNull();

        var upload = await agent.CallAsync("upload_image", new { deck, name = "story-map.png", contentType = "image/png" });
        upload.GetProperty("method").GetString().ShouldBe("PUT");
        await PutImageAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), PngImage.Gradient(640, 360));
        var imageId = upload.GetProperty("image").GetString().ShouldNotBeNull();

        await AddAsync(agent, deck, "title", Title);
        await AddAsync(agent, deck, "divider", Divider, part: 1);
        (await AddAsync(agent, deck, "bullets", Bullets, part: 1)).GetProperty("steps").GetInt32().ShouldBe(BULLETS_STEPS);
        await AddAsync(agent, deck, "big-number", BigNumber("12 wks"), part: 1);
        await AddAsync(agent, deck, "image", Image(imageId), part: 2);
        await AddAsync(agent, deck, "compare", Compare, part: 2);
        await AddAsync(agent, deck, "numbered-cards", NumberedCards, part: 2);
        await AddAsync(agent, deck, "table", Table, part: 3);
        await AddAsync(agent, deck, "close", Close);
        await AddAsync(agent, deck, "chart", Chart, part: 3);

        // The chart went in after the close: move it in front. Then an aside goes in at 4 and comes out again.
        (await agent.CallAsync("move_slide", new { deck, slide = 10, to = 9 })).GetProperty("slide").GetInt32().ShouldBe(9);
        await AddAsync(agent, deck, "quote", Aside, position: 4);
        (await agent.CallAsync("delete_slide", new { deck, slide = 4 })).GetProperty("slides").GetInt32().ShouldBe(SLIDES);

        // The big number was a guess: fix it with the revision get_deck gives now, after the slides around it moved.
        var bigNumber = await SlideAsync(agent, deck, 4);
        bigNumber.GetProperty("layout").GetString().ShouldBe("big-number");
        await agent.CallAsync("update_slide", new { deck, slide = 4, revision = bigNumber.GetProperty("revision").GetInt64(), slots = BigNumber("11 wks") });

        await agent.CallAsync("set_notes", new
        {
            deck,
            notes = new[] { new { slide = 1, text = "Welcome everyone." }, new { slide = BULLETS_SLIDE, text = "One point per click." } },
        });

        var built = await agent.CallAsync("get_deck", new { deck });
        var slides = built.GetProperty("slides").EnumerateArray().ToList();
        slides.Select(s => s.GetProperty("layout").GetString()).ShouldBe(LAYOUTS.Cast<string?>());
        slides[3].GetProperty("slots").GetProperty("value").GetString().ShouldBe("11 wks");
        slides[BULLETS_SLIDE - 1].GetProperty("notes").GetString().ShouldBe("One point per click.");

        var shared = await agent.CallAsync("set_sharing", new { deck, level = "link" });
        shared.GetProperty("level").GetString().ShouldBe("link");
        var playUrl = (await agent.CallAsync("get_deck_url", new { deck })).GetProperty("url").GetString().ShouldNotBeNull();
        playUrl.ShouldStartWith($"{AcceptanceApp.BaseUrl}/d/{deck}/");
        var deepLink = (await agent.CallAsync("get_deck_url", new { deck, slide = BULLETS_SLIDE, step = 2 })).GetProperty("url").GetString().ShouldNotBeNull();
        deepLink.ShouldBe($"{playUrl}#{BULLETS_SLIDE}.2");
        return (deck, playUrl, deepLink);
    }

    // Sam presents in a 1920×1080 window: each slide shows in turn under the arrow keys, inside its frame, the bullets one
    // per press, and the deep link opens part way through the bullets.
    private async Task PresentAt1920Async(string deckId, string playUrl, string deepLink)
    {
        var player = new PlayerPage(Page);
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync(playUrl);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Image(IMAGE_SLIDE)).ToHaveAttributeAsync("src", new Regex("/api/images/img_"));
        await player.FontsReadyAsync();
        await player.ImageLoadedAsync(IMAGE_SLIDE);

        var fit = await Eventually.ReadAsync(player.FitAsync, f => IsEven(f) && f.Width <= 1920.5);
        (fit.Width / fit.Height).ShouldBe(1920.0 / 1080, 0.01);
        fit.Width.ShouldBeLessThanOrEqualTo(1920.5);

        var overflow = new List<string>();
        for (var number = 1; number <= SLIDES; number++)
        {
            await Expect(player.Count).ToHaveTextAsync($"{number} / {SLIDES}");
            await Expect(player.Layer(number)).ToHaveAttributeAsync("data-active", "true");
            await Expect(player.Slide(number)).ToHaveAttributeAsync("data-layout", LAYOUTS[number - 1]);
            overflow.AddRange((await OverflowSettledAsync(player, number)).Select(o => $"slide {number} ({LAYOUTS[number - 1]}): {o}"));
            if (number == BULLETS_SLIDE)
            {
                for (var step = 0; step < BULLETS_STEPS; step++)
                {
                    await Expect(player.RevealedBullets(number)).ToHaveCountAsync(step);
                    await Page.Keyboard.PressAsync("ArrowRight");
                }
                await Expect(player.RevealedBullets(number)).ToHaveCountAsync(BULLETS_STEPS);
                await Expect(Page).ToHaveURLAsync($"{playUrl}#{number}.{BULLETS_STEPS}");
            }
            if (number < SLIDES)
            {
                await Page.Keyboard.PressAsync("ArrowRight");
            }
        }
        await Expect(player.Count).ToHaveTextAsync($"{SLIDES} / {SLIDES}");
        overflow.ShouldBeEmpty();

        await Page.GotoAsync(deepLink);
        await Expect(player.Count).ToHaveTextAsync($"{BULLETS_SLIDE} / {SLIDES}");
        await Expect(player.RevealedBullets(BULLETS_SLIDE)).ToHaveCountAsync(2);
        await Expect(player.Bullets(BULLETS_SLIDE)).ToHaveCountAsync(BULLETS_STEPS);
        await Expect(player.Root).ToHaveAttributeAsync("data-deck-id", deckId);
    }

    // A guest, signed out, opens the link on a 390×844 phone: the canvas scales evenly to the phone's width with no
    // sideways scroll, every slide still fits its frame, and a swipe moves on.
    private async Task PresentOnPhoneAsGuestAsync(string playUrl)
    {
        await using var phone = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
            HasTouch = true,
            IsMobile = true,
        });
        var page = await phone.NewPageAsync();
        var player = new PlayerPage(page);

        await page.GotoAsync(playUrl);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await player.FontsReadyAsync();
        await player.ImageLoadedAsync(IMAGE_SLIDE);

        var fit = await Eventually.ReadAsync(
            player.FitAsync,
            f => IsEven(f) && f.Left >= -0.5 && f.Right <= 390.5 && f.ScrollWidth <= f.ViewportWidth);
        (fit.Width / fit.Height).ShouldBe(1920.0 / 1080, 0.01);
        fit.Left.ShouldBeGreaterThanOrEqualTo(-0.5);
        fit.Right.ShouldBeLessThanOrEqualTo(390.5);
        fit.ScrollWidth.ShouldBeLessThanOrEqualTo(fit.ViewportWidth);
        var overflow = new List<string>();
        for (var number = 1; number <= SLIDES; number++)
        {
            overflow.AddRange((await OverflowSettledAsync(player, number)).Select(o => $"phone, slide {number} ({LAYOUTS[number - 1]}): {o}"));
        }
        overflow.ShouldBeEmpty();

        await player.SwipeLeftAsync();
        await Expect(player.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
    }

    // The canvas keeps 16:9 as it scales.
    private static bool IsEven(PlayerPage.CanvasFit fit) => Math.Abs(fit.Width / fit.Height - 1920.0 / 1080) <= 0.01;

    // The overflow probe, retried while layout settles; the last reading is what the test asserts.
    private static Task<string[]> OverflowSettledAsync(PlayerPage player, int number) =>
        Eventually.ReadAsync(() => player.OverflowAsync(number), found => found.Length == 0);

    private static Task<JsonElement> AddAsync(AgentSession agent, string deck, string layout, object slots, int? part = null, int? position = null)
    {
        var args = new Dictionary<string, object> { ["deck"] = deck, ["layout"] = layout, ["slots"] = slots };
        if (part != null)
        {
            args["part"] = part;
        }
        if (position != null)
        {
            args["position"] = position;
        }
        return agent.CallAsync("add_slide", args);
    }

    private static async Task<JsonElement> SlideAsync(AgentSession agent, string deck, int number) =>
        (await agent.CallAsync("get_deck", new { deck })).GetProperty("slides").EnumerateArray()
            .Single(s => s.GetProperty("number").GetInt32() == number);

    // The agent PUTs the raw bytes to the one-time upload URL, with no auth header, as upload_image says.
    private static async Task PutImageAsync(string uploadUrl, byte[] png)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var response = await http.PutAsync(uploadUrl, content);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
}
