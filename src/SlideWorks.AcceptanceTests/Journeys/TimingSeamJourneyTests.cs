using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Features.OAuth.Registration;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Journeys;

// The acceptance app's timing seams on the real app: AcceptanceApp.Clock steps the app's clock past a rate-limit window,
// and AcceptanceApp.Hubs aborts the presenter's hub connection from the server so the browser reconnects on its own,
// with no Playwright WebSocket route. The app is shared, so TearDown puts the seams back for the journeys after these,
// and the second journey checks that the first left the journeys after it a whole registration budget.
public class TimingSeamJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;

    // One second more than the limiter's one-minute window.
    private static readonly TimeSpan PAST_THE_WINDOW = TimeSpan.FromSeconds(61);

    // How far TearDown steps the clock before it resets it: past the end of the window the clock journey opens 61 s ahead
    // (61 + 60 s) and so past the limiter's next sweep, which is never more than a window after the latest time it saw.
    private static readonly TimeSpan PAST_EVERY_WINDOW = TimeSpan.FromMinutes(3);

    // An address no journey calls from (TEST-NET-1, RFC 5737), for the check that runs the limiter's sweep.
    private static readonly IPAddress NO_JOURNEY = IPAddress.Parse("192.0.2.1");

    // The limiter keeps a window per caller address, and every journey calls from 127.0.0.1. The clock journey leaves
    // 127.0.0.1 a window that starts 61 s ahead of the real clock, so after a plain reset every journey would share it,
    // and its budget, for about two real minutes. Only the limiter's own sweep drops a window, and only one that has
    // ended. So TearDown steps the clock past every window, makes one check from an address no journey uses (which
    // sweeps first), and only then resets the clock.
    [TearDown]
    public void ResetSeams()
    {
        AcceptanceApp.Clock.Advance(PAST_EVERY_WINDOW);
        AcceptanceApp.Services.GetRequiredService<RegistrationLimiter>().TryAcquire(NO_JOURNEY, out _);
        AcceptanceApp.ResetSeams();
    }

    // The registration limit is per caller address, and every request here comes from 127.0.0.1. Earlier journeys may
    // already have used part of the window, so register until refused rather than counting to the limit.
    [Test, Order(1)]
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

        AcceptanceApp.Clock.Advance(PAST_THE_WINDOW);

        using var again = await RegisterAsync(http);
        again.StatusCode.ShouldBe(HttpStatusCode.Created, await again.Content.ReadAsStringAsync());
    }

    // What the journeys after the clock journey get in the same app: a whole window of registrations over HTTP from
    // 127.0.0.1. Had the clock journey left its window behind, it would hold one registration and the tenth here would
    // be refused. These are made with the wall clock one window back, so the window they fill has already ended on the
    // real clock and costs the journeys after this one nothing.
    [Test, Order(2)]
    public async Task OAuthRegister_WhenTheClockJourneyHasRun_ShouldAllowAWholeWindow()
    {
        AcceptanceApp.Clock.StepWall(-PAST_THE_WINDOW);
        using var http = new HttpClient { BaseAddress = new Uri(AcceptanceApp.BaseUrl) };

        for (var registration = 1; registration <= RegistrationLimiter.PER_MINUTE; registration++)
        {
            using var response = await RegisterAsync(http);
            response.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                $"registration {registration} of {RegistrationLimiter.PER_MINUTE}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    // The server drops the presenter's hub connection; the window reconnects by itself and carries on presenting.
    [Test, Order(3)]
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
