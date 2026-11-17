using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Presenting;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// The lockout from a guesser's side, black-box over the real sessions, relay and hub (J3, D20): a phone already following keeps
// getting moves after its caller used up the miss limit, while a new code is refused. The acceptance app shares one address
// among every browser and one real clock, so the same check there would lock out other tests for a minute; here the clock is
// the fixture's. SW-4's registry is stood in by a settable IPresentations, as in AudienceRelayTests.
[TestFixture]
[CancelAfter(60000)]
public class JoinLimitFollowingTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    [Test]
    public async Task Follow_WhenCallerHasUsedUpItsMisses_ShouldKeepReceivingMovesButRefuseNewLookups()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await PresentingSeed.CreateDeckAsync(agent);
        var live = await (await Client.PostAsync($"/api/decks/{deck}/live", null)).Content.ReadFromJsonAsync<JsonElement>();
        var code = live.GetProperty("code").GetString()!;
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        using var anonymous = ClientFor(TestUser.Anonymous);
        for (var i = 0; i < 10; i++)
        {
            (await anonymous.GetAsync("/api/join/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var refused = await anonymous.GetAsync($"/api/join/{code}");
        var state = SettablePresentations.At(deck, 2, 0, false);
        Factory.Services.GetRequiredService<SettablePresentations>().Set(state);
        foreach (var observer in Factory.Services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
        var moved = await guest.NextAudienceAsync();

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync("JoinAudience", code))).Message.ShouldContain("too_many_attempts", Case.Sensitive);
        guest.Connection.State.ShouldBe(HubConnectionState.Connected);
        moved.GetProperty("type").GetString().ShouldBe("audienceState");
        moved.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
    }
}
