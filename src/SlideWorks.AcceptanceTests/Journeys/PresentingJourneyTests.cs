using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Client;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-4 journeys for presenting from the laptop, on the real app and the real hub. Sam's talk is made over MCP:
// thirteen bullets slides in two parts, each with notes, and slide 12 a deep dive. The owner opens Presenter view from
// the deck page's Present menu, which opens the audience window as a popup next to it; the presenter window's keys
// then drive both. A share-link viewer opening the audience URL gets the plain player and never the notes.
public class PresentingJourneyTests : AcceptanceTest
{
    private const int SLIDES = 13;
    private const int DEEP_DIVE = 12;

    // Criteria: Presenter view from the deck page opens the audience window; keys move it; B blacks it out; O then a
    // click jumps both windows; D then → skips the deep dive; the audience URL is P3's audienceUrl and the phone
    // dialog's link is P3's remoteUrl.
    [Test]
    public async Task PresenterView_WhenOwnerOpensItFromTheDeckPage_ShouldDriveTheAudienceWindow()
    {
        var talk = await SeedTalkAsync($"Presenting {Guid.NewGuid():N}");
        await SignIn.AsAsync(Page, TestUser.Sam);
        var viewResponse = await Page.APIRequest.GetAsync($"/api/decks/{talk.Id}/presenter");
        viewResponse.Status.ShouldBe(200, await viewResponse.TextAsync());
        var view = (await viewResponse.JsonAsync())!.Value;

        // 1. The deck page's Present menu → Presenter view: this tab becomes the presenter window, and the audience
        // window opens as a popup in the same click.
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(talk.Id);
        await deckPage.PresentMenu.ClickAsync();
        var audienceTab = await Page.RunAndWaitForPopupAsync(() => deckPage.PresentMenuPresenterView.ClickAsync());
        var presenter = new PresenterPage(Page);
        var audience = new PlayerPage(audienceTab);
        await Expect(Page).ToHaveURLAsync(AcceptanceApp.BaseUrl + PresenterPage.PathFor(talk.Id));
        await presenter.IsLiveAsync();
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Expect(audience.Controls).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.NotesBody).ToContainTextAsync(NotesOf(1));

        // The audience window is P3's audienceUrl. The player writes #slide.step into the hash as it follows, so the
        // hash is left out of the comparison.
        new Uri(audienceTab.Url).GetLeftPart(UriPartial.Query).ShouldBe(view.GetProperty("audienceUrl").GetString());

        // 2. Keys in the presenter window move the audience window.
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(audience.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        await presenter.PressAsync(" ");
        await Expect(presenter.Count).ToHaveTextAsync($"3 / {SLIDES}");
        await Expect(audience.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        await presenter.PressAsync("ArrowLeft");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(audience.Layer(2)).ToHaveAttributeAsync("data-active", "true");

        // 3. B blacks the audience window out, and B again brings the slide back.
        await presenter.PressAsync("b");
        await Expect(audience.Blackout).ToBeVisibleAsync();
        await Expect(presenter.BlackBadge).ToBeVisibleAsync();
        await presenter.PressAsync("b");
        await Expect(audience.Blackout).ToHaveCountAsync(0);
        await Expect(presenter.BlackBadge).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(audience.Layer(2)).ToHaveAttributeAsync("data-active", "true");

        // 4. O opens the overview; clicking slide 11 jumps both windows and closes it.
        await presenter.PressAsync("o");
        await Expect(presenter.Overview).ToBeVisibleAsync();
        await presenter.OverviewSlide(11).ClickAsync();
        await Expect(presenter.Overview).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"11 / {SLIDES}");
        await Expect(audience.Layer(11)).ToHaveAttributeAsync("data-active", "true");

        // 5. D turns on skip deep dives; → from 11 then steps over the deep dive at 12 to 13.
        await presenter.PressAsync("d");
        await Expect(presenter.SkipDeepDivesTool).ToHaveAttributeAsync("aria-pressed", "true");
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"{DEEP_DIVE + 1} / {SLIDES}");
        await Expect(audience.Layer(DEEP_DIVE + 1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(audience.Layer(DEEP_DIVE)).ToHaveAttributeAsync("data-active", "false");

        // 6. The phone dialog shows P3's remoteUrl.
        await presenter.PhoneTool.ClickAsync();
        await Expect(presenter.PhoneConnect).ToBeVisibleAsync();
        await Expect(presenter.PhoneConnectUrl).ToHaveTextAsync(view.GetProperty("remoteUrl").GetString()!);

        // 7. The deck page's own phone dialog, from the Present menu's "Control from your phone".
        await deckPage.GotoAsync(talk.Id);
        await deckPage.PresentMenu.ClickAsync();
        await deckPage.PresentMenuPhone.ClickAsync();
        await Expect(deckPage.PhoneConnect).ToBeVisibleAsync();
        await Expect(deckPage.PhoneConnectUrl).ToHaveTextAsync(view.GetProperty("remoteUrl").GetString()!);
    }

    // Criterion: a share-link viewer who opens /d/:id/:slug?present while the owner presents gets the plain player
    // (not audience mode: the control bar shows and it doesn't follow the talk), and no notes reach the page: not in
    // the DOM, not in any API response, not in any hub message.
    [Test]
    public async Task AudienceUrl_WhenShareLinkViewerOpensItWhileOwnerPresents_ShouldShowThePlainPlayerWithoutNotes()
    {
        var talk = await SeedTalkAsync($"Shared talk {Guid.NewGuid():N}");
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            await AgentSeed.CallAsync(agent, "set_sharing", new() { ["deck"] = talk.Id, ["level"] = "link" });
        }

        // The owner presents, on slide 3.
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(talk.Id);
        await presenter.IsLiveAsync();
        await presenter.JumpToAsync(3);
        await Expect(presenter.Count).ToHaveTextAsync($"3 / {SLIDES}");

        // The viewer, anonymous in a browser of their own, opens the audience URL.
        await using var context = await Browser.NewContextAsync(ContextOptions());
        var viewerTab = await context.NewPageAsync();
        // Playwright raises these events off the test thread, so the lists are concurrent.
        var apiBodies = new ConcurrentQueue<IResponse>();
        var hubFrames = new ConcurrentQueue<string>();
        viewerTab.Response += (_, response) =>
        {
            if (new Uri(response.Url).AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
            {
                apiBodies.Enqueue(response);
            }
        };
        viewerTab.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) => hubFrames.Enqueue(frame.Text ?? "");
        var viewer = new PlayerPage(viewerTab);
        await viewerTab.GotoAsync($"/d/{talk.Id}/{talk.Slug}?present");

        await Expect(viewer.Player).ToBeVisibleAsync();
        await Expect(viewer.AudienceMode).ToHaveCountAsync(0);
        await Expect(viewer.Controls).ToBeVisibleAsync();
        await Expect(viewer.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(viewer.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(0);

        var html = await viewerTab.ContentAsync();
        var bodies = apiBodies.ToArray();
        var frames = hubFrames.ToArray();
        bodies.ShouldNotBeEmpty();
        frames.ShouldNotBeEmpty();
        foreach (var number in Enumerable.Range(1, SLIDES))
        {
            html.ShouldNotContain(NotesOf(number));
            foreach (var response in bodies)
            {
                (await response.TextAsync()).ShouldNotContain(NotesOf(number), customMessage: response.Url);
            }
            foreach (var frame in frames)
            {
                frame.ShouldNotContain(NotesOf(number));
            }
        }
    }

    private static string NotesOf(int number) => $"Speaker cue {number}: pause for a question here.";

    // Sam's talk, made the way an agent makes it: create_deck with two parts, then thirteen bullets slides (no
    // reveals, so each is one step) with notes, slides 1-6 in part 1 and 7-13 in part 2, slide 12 a deep dive.
    private static async Task<Talk> SeedTalkAsync(string title)
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new()
        {
            ["title"] = title,
            ["parts"] = new[] { "The Cost", "Ship It" },
        });
        var id = created.GetProperty("deck").GetString()!;
        var slug = new Uri(created.GetProperty("url").GetString()!).Segments[^1];
        foreach (var number in Enumerable.Range(1, SLIDES))
        {
            await AddSlideAsync(agent, id, number);
        }
        return new Talk(id, slug);
    }

    private static async Task AddSlideAsync(McpClient agent, string deck, int number)
    {
        var slots = JsonSerializer.SerializeToElement(new
        {
            headline = $"Point {number} of the talk",
            items = new[] { $"First thing about point {number}", $"Second thing about point {number}" },
        });
        await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "bullets",
            ["part"] = number <= 6 ? 1 : 2,
            ["slots"] = slots,
            ["notes"] = NotesOf(number),
            ["deep_dive"] = number == DEEP_DIVE,
        });
    }

    private sealed record Talk(string Id, string Slug);
}
