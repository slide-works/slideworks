using System.Net;
using System.Net.Http.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Features.OAuth.Registration;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Journeys;

// The acceptance app's timing seams on the real app: AcceptanceApp.Clock steps the app's clock past a rate-limit window,
// and AcceptanceApp.Hubs aborts the presenter's hub connection from the server so the browser reconnects on its own,
// with no Playwright WebSocket route. The app is shared, so TearDown puts the seams back for the journeys after these.
public class TimingSeamJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;

    [TearDown]
    public void ResetSeams() => AcceptanceApp.ResetSeams();

    // The registration limit is per caller address, and every request here comes from 127.0.0.1. Earlier journeys may
    // already have used part of the window, so register until refused rather than counting to the limit.
    [Test]
    public async Task OAuthRegister_WhenRefusedAndTheClockStepsPastTheWindow_ShouldRegisterAgain()
    {
        using var http = new HttpClient { BaseAddress = new Uri(AcceptanceApp.BaseUrl) };
        var refused = false;
        for (var attempt = 0; attempt <= RegistrationLimiter.PER_MINUTE && !refused; attempt++)
        {
            using var response = await RegisterAsync(http);
            refused = response.StatusCode == HttpStatusCode.TooManyRequests;
        }
        refused.ShouldBeTrue($"registration should be refused within {RegistrationLimiter.PER_MINUTE + 1} calls");

        AcceptanceApp.Clock.Advance(TimeSpan.FromSeconds(61));

        using var again = await RegisterAsync(http);
        again.StatusCode.ShouldBe(HttpStatusCode.Created, await again.Content.ReadAsStringAsync());
    }

    // The server drops the presenter's hub connection; the window reconnects by itself and carries on presenting.
    [Test]
    public async Task PresenterWindow_WhenTheServerAbortsItsHubConnection_ShouldReconnectAndKeepPresenting()
    {
        var id = await CreateDeckAsync();
        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(id);
        await presenter.IsLiveAsync();
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        var opened = AcceptanceApp.Hubs.Opens.CallsFor(HubAborts.OPENED);

        AcceptanceApp.Hubs.AbortAll().ShouldBeGreaterThan(0, "the presenter window should have a hub connection to abort");
        await AcceptanceApp.Hubs.Opens.WhenCalledAsync(HubAborts.OPENED, opened + 1, TimeSpan.FromSeconds(30));

        await Expect(presenter.Reconnecting).ToBeHiddenAsync();
        await presenter.IsLiveAsync();
        await presenter.PressAsync("ArrowRight");
        await Expect(presenter.Count).ToHaveTextAsync($"2 / {SLIDES}");
        await Expect(presenter.Error).ToHaveCountAsync(0);
    }

    // One dynamic client registration (RFC 7591), with the body AgentOAuthClient.RegisterAsync sends.
    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient http)
        => await http.PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["client_name"] = "Timing seam",
            ["redirect_uris"] = new[] { AgentOAuthClient.CALLBACK },
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
            ["response_types"] = new[] { "code" },
            ["token_endpoint_auth_method"] = "none",
        });

    // Sam's deck with SLIDES quote slides, made the way an agent makes it.
    private static async Task<string> CreateDeckAsync()
    {
        string id;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Timing seam {Guid.NewGuid():N}" });
            id = created.GetProperty("deck").GetString()!;
        }
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, Enumerable.Range(1, SLIDES).Select(n => $"Quote number {n}.").ToArray());
        return id;
    }
}
