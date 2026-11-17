using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-5's network journeys on the real app: a phone whose network blocks WebSockets still follows a live talk, and the join
// host (SLIDEWORKS_JOIN_URL, the same server under http://localhost:{port}) serves the keypad and the guest page and sends
// every other path to the public URL. Sam owns each deck and moves it in the plain player (the Page); the phone is a second
// browser context. A blocked transport is a test-only init script that makes the page's constructor throw: Playwright's
// RouteWebSocketAsync crashes the .NET 1.62 binding when SignalR closes its socket, so it is not used.
public class Sw5NetworkJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;

    // Every WebSocket (and optionally EventSource) the page tries is counted, then refused as a blocked network would.
    private const string BLOCK_WEBSOCKET =
        """
        (() => {
          window.__attempts = { webSocket: 0, eventSource: 0 };
          window.WebSocket = function () {
            window.__attempts.webSocket++;
            throw new Error('WebSocket is blocked on this network');
          };
        })()
        """;

    private const string BLOCK_EVENT_SOURCE =
        """
        (() => {
          window.EventSource = function () {
            window.__attempts.eventSource++;
            throw new Error('EventSource is blocked on this network');
          };
        })()
        """;

    private const string COUNT_EVENT_SOURCE =
        """
        (() => {
          const Native = window.EventSource;
          window.EventSource = class extends Native {
            constructor(...args) {
              super(...args);
              window.__attempts.eventSource++;
            }
          };
        })()
        """;

    private static string JoinBaseUrl => AcceptanceApp.JoinUrl;

    // D7: with WebSockets blocked the phone falls back to Server-Sent Events, follows two moves and shows the thank-you.
    [Test]
    public async Task Follow_WhenWebSocketsAreBlocked_ShouldUseServerSentEventsAndShowTheThankYou()
    {
        var (deckId, code) = await CreateLiveDeckAsync();
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await using var phoneContext = await NewPhoneContextAsync(JoinBaseUrl);
        await phoneContext.AddInitScriptAsync(BLOCK_WEBSOCKET + ";" + COUNT_EVENT_SOURCE);
        var log = RecordHubRequests(phoneContext);

        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoAsync(code);
        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "3");
        await EndLiveAsync(deckId);

        await Expect(phone.Ended).ToBeVisibleAsync();
        var (webSockets, eventSources) = await AttemptsAsync(phone);
        webSockets.ShouldBeGreaterThanOrEqualTo(1, "the page should have tried a WebSocket, and been refused every time");
        eventSources.ShouldBeGreaterThanOrEqualTo(1, "the page should have opened an EventSource");
        var requests = log.Snapshot();
        requests.ShouldContain(r => r.Method == "GET" && r.Accept.Contains("text/event-stream"), "the phone should hold a Server-Sent Events stream");
        await AssertNegotiatedOnTheJoinHostWithItsCookieAsync(requests);
        requests.ShouldContain(r => r.Method == "POST" && !r.Url.Contains("negotiate"), "the phone's invocations should go as POSTs beside the stream");
    }

    // D7: with WebSockets and EventSource both blocked the phone long-polls, and still follows and shows the thank-you.
    [Test]
    public async Task Follow_WhenWebSocketsAndEventSourceAreBlocked_ShouldLongPollAndShowTheThankYou()
    {
        var (deckId, code) = await CreateLiveDeckAsync();
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await using var phoneContext = await NewPhoneContextAsync(JoinBaseUrl);
        await phoneContext.AddInitScriptAsync(BLOCK_WEBSOCKET + ";" + BLOCK_EVENT_SOURCE);
        var log = RecordHubRequests(phoneContext);

        var phone = new GuestPage(await phoneContext.NewPageAsync());
        await phone.GotoAsync(code);
        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "3");
        await EndLiveAsync(deckId);

        await Expect(phone.Ended).ToBeVisibleAsync();
        var (webSockets, eventSources) = await AttemptsAsync(phone);
        webSockets.ShouldBeGreaterThanOrEqualTo(1, "the page should have tried a WebSocket, and been refused every time");
        eventSources.ShouldBeGreaterThanOrEqualTo(1, "the page should have tried an EventSource, and been refused every time");
        var requests = log.Snapshot();
        var polls = requests.Where(r => r.Method == "GET" && !r.Accept.Contains("text/event-stream") && !r.Url.Contains("negotiate")).ToList();
        requests.ShouldNotContain(r => r.Accept.Contains("text/event-stream"), "no stream should have opened");
        polls.Count.ShouldBeGreaterThanOrEqualTo(3, "long polling should have made repeated GET polls (the join and two moves)");
        await AssertNegotiatedOnTheJoinHostWithItsCookieAsync(requests);
    }

    // J7: the join host's bare address lands on the keypad.
    [Test]
    public async Task JoinHost_WhenRoot_ShouldLandOnTheKeypad()
    {
        await using var context = await NewPhoneContextAsync(JoinBaseUrl);
        var page = await context.NewPageAsync();
        var keypad = new JoinCodePage(page);

        await page.GotoAsync("/");

        await keypad.IsLoadedAsync();
        page.Url.ShouldBe($"{JoinBaseUrl}/join");
    }

    // J7, D12: a code typed on the join host's keypad reaches the guest page on the join host, which sets the guest cookie
    // on that host and follows over the hub there.
    [Test]
    public async Task JoinHost_WhenCodeIsTypedOnTheKeypad_ShouldFollowOnTheJoinHostWithItsOwnCookie()
    {
        var (deckId, code) = await CreateLiveDeckAsync();
        await using var context = await NewPhoneContextAsync(JoinBaseUrl);
        var page = await context.NewPageAsync();
        var keypad = new JoinCodePage(page);
        var phone = new GuestPage(page);
        await page.GotoAsync(JoinCodePage.PATH);
        await keypad.IsLoadedAsync();

        await keypad.TypeAsync(code);

        await phone.IsFollowingAsync();
        page.Url.ShouldBe($"{JoinBaseUrl}/j/{code}");
        var cookies = await context.CookiesAsync([JoinBaseUrl]);
        cookies.ShouldContain(c => c.Name == "sw_guest" && c.Domain == "localhost" && c.HttpOnly);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deckId);
        await Expect(player.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "2");
    }

    // J7: /{6 digits} is the join host's short link.
    [Test]
    public async Task JoinHost_WhenSixDigitPath_ShouldFollow()
    {
        var (_, code) = await CreateLiveDeckAsync();
        await using var context = await NewPhoneContextAsync(JoinBaseUrl);
        var page = await context.NewPageAsync();
        var phone = new GuestPage(page);

        await phone.GotoBareAsync(code);

        await phone.IsFollowingAsync();
        await Expect(phone.Slide).ToHaveAttributeAsync("data-slide", "1");
        page.Url.ShouldBe($"{JoinBaseUrl}/{code}", "the short link should be served on the join host, not sent to the public URL");
    }

    // J7, D21: the thank-you's slides link is absolute on the public URL for a deck shared by link, and absent for a private
    // one, even though the page is served from the join host.
    [TestCase("link", true)]
    [TestCase("private", false)]
    public async Task JoinHost_WhenSessionEnded_ShouldLinkToThePublicUrlOnlyForALinkSharedDeck(string level, bool linked)
    {
        var (deckId, code) = await CreateLiveDeckAsync();
        var shared = await Page.APIRequest.PutAsync($"/api/decks/{deckId}/sharing", new APIRequestContextOptions
        {
            DataObject = new { level, viewers = Array.Empty<string>() },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        shared.Status.ShouldBe(200, await shared.TextAsync());
        await EndLiveAsync(deckId);
        await using var context = await NewPhoneContextAsync(JoinBaseUrl);
        var page = await context.NewPageAsync();
        var phone = new GuestPage(page);

        await phone.GotoBareAsync(code);

        await Expect(phone.Ended).ToBeVisibleAsync();
        page.Url.ShouldBe($"{JoinBaseUrl}/{code}", "the ended screen should be served on the join host, not sent to the public URL");
        if (linked)
        {
            await Expect(phone.EndedSlides).ToHaveAttributeAsync("href", new Regex($"^{Regex.Escape(AcceptanceApp.BaseUrl)}/d/{deckId}/[^/?#]+$"));
        }
        else
        {
            await Expect(phone.EndedSlides).ToHaveCountAsync(0);
        }
    }

    // J7: an app path on the join host goes to the same path on the public URL.
    [Test]
    public async Task JoinHost_WhenAppPath_ShouldRedirectToThePublicUrl()
    {
        await using var context = await NewPhoneContextAsync(JoinBaseUrl);

        var response = await context.APIRequest.GetAsync("/decks?page=2", new APIRequestContextOptions { MaxRedirects = 0 });

        response.Status.ShouldBe(302);
        response.Headers["location"].ShouldBe($"{AcceptanceApp.BaseUrl}/decks?page=2");
    }

    // Sam's deck of SLIDES quotes, gone live. Sam is signed in on the test's Page and stays so: the player and the End call use it.
    private async Task<(string DeckId, string Code)> CreateLiveDeckAsync()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var deckId = await PostAsync("/api/decks", new { title = $"Network {Guid.NewGuid():N}" }, 201, "id");
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deckId, "Start small.", "Keep going.", "Then stop.");
        var code = await PostAsync($"/api/decks/{deckId}/live", null, 200, "code");
        return (deckId, code);
    }

    private async Task EndLiveAsync(string deckId)
    {
        var response = await Page.APIRequest.DeleteAsync($"/api/decks/{deckId}/live", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(204, await response.TextAsync());
    }

    private async Task<string> PostAsync(string path, object? body, int status, string field)
    {
        var response = await Page.APIRequest.PostAsync(path, new APIRequestContextOptions
        {
            DataObject = body,
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(status, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString()!;
    }

    private async Task<IBrowserContext> NewPhoneContextAsync(string baseUrl) => await Browser.NewContextAsync(new BrowserNewContextOptions
    {
        BaseURL = baseUrl,
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    // The hub requests the phone makes after negotiating: what the transport really was, whatever the page shows.
    private static HubRequests RecordHubRequests(IBrowserContext context)
    {
        var log = new HubRequests();
        context.Request += (_, request) =>
        {
            if (request.Url.Contains("/hubs/deck"))
            {
                log.Add(new HubRequest(request.Method, request.Url, request.Headers.GetValueOrDefault("accept") ?? string.Empty, request));
            }
        };
        return log;
    }

    private static async Task<(int WebSocket, int EventSource)> AttemptsAsync(GuestPage phone)
    {
        var attempts = await phone.Root.Page.EvaluateAsync<JsonElement>("() => window.__attempts");
        return (attempts.GetProperty("webSocket").GetInt32(), attempts.GetProperty("eventSource").GetInt32());
    }

    private sealed record HubRequest(string Method, string Url, string Accept, IRequest Request);

    // J7, D12: the hub was reached on the join host, and the cookie that host set went with the negotiate request.
    private static async Task AssertNegotiatedOnTheJoinHostWithItsCookieAsync(IReadOnlyList<HubRequest> requests)
    {
        var negotiate = requests.First(r => r.Url.Contains("negotiate"));
        negotiate.Url.ShouldStartWith($"{JoinBaseUrl}/hubs/deck");
        (await negotiate.Request.AllHeadersAsync()).GetValueOrDefault("cookie", string.Empty).ShouldContain("sw_guest=");
    }

    private sealed class HubRequests
    {
        private readonly List<HubRequest> _seen = [];

        public void Add(HubRequest request)
        {
            lock (_seen)
            {
                _seen.Add(request);
            }
        }

        public IReadOnlyList<HubRequest> Snapshot()
        {
            lock (_seen)
            {
                return [.. _seen];
            }
        }
    }
}
