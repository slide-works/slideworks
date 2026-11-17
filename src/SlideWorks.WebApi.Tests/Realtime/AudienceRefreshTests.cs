using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Presenting;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// IAudienceRelay.RefreshAsync (V3) on the real relay, sessions and hub: it composes the deck's state again and publishes
// it, with no presentation change behind it. SW-4's registry is stood in by a settable IPresentations that is changed
// without telling the observers, so only the refresh can carry the new position.
[TestFixture]
[CancelAfter(30000)]
public class AudienceRefreshTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    [Test]
    public async Task Refresh_WhenCalled_ShouldPublishTheCurrentAudienceState()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await PresentingSeed.CreateDeckAsync(agent);
        var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        Factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, slide: 2));

        await Factory.Services.GetRequiredService<IAudienceRelay>().RefreshAsync(deck);
        var pushed = await guest.NextAudienceAsync();

        joined.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(1);
        pushed.GetProperty("type").GetString().ShouldBe("audienceState");
        pushed.GetProperty("phase").GetString().ShouldBe("live");
        pushed.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        pushed.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
    }
}
