using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// The slide a guest follows carries the surface it is drawn on (K6): the stored one, else the layout's own, never null.
// The deck is seeded through the public add_slide tool, the talk is made live through the route and the guest's state is
// read as the phone gets it on joining.
[TestFixture]
[CancelAfter(30000)]
public class AudienceSurfaceTests : ApiTestFixture
{
    private static readonly string[] _currentKeys = ["index", "count", "layout", "slots", "steps", "step", "black", "surface"];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    [Test]
    public async Task Guest_WhenTheSlideOnScreenIsStoredDark_ShouldCarryDark()
    {
        var current = await GuestSeesAsync("statement", new { statement = "Ship smaller" }, "dark");

        current.GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task Guest_WhenTheSlideHasNoSurface_ShouldCarryTheLayoutsOwn()
    {
        var statement = await GuestSeesAsync("statement", new { statement = "Ship smaller" }, null);
        var title = await GuestSeesAsync("title", new { title = "Ship smaller" }, null);

        statement.GetProperty("surface").GetString().ShouldBe("light");
        title.GetProperty("surface").GetString().ShouldBe("dark");
    }

    [Test]
    public async Task Guest_WhenTheSlideOnScreen_ShouldStillCarryNoNotes()
    {
        var current = await GuestSeesAsync("statement", new { statement = "Ship smaller" }, "dark", notes: "Private note");

        current.EnumerateObject().Select(p => p.Name).ShouldBe(_currentKeys, ignoreOrder: true);
        current.GetRawText().ShouldNotContain("Private note", Case.Sensitive);
    }

    private async Task<JsonElement> GuestSeesAsync(string layout, object slots, string? surface, string? notes = null)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var (isError, json) = await agent.CallToolAsync("add_slide", new { deck, layout, slots, surface, notes });
        isError.ShouldBeFalse(json.ToString());
        var live = await Client.PostAsync($"/api/decks/{deck}/live", null);
        live.EnsureSuccessStatusCode();
        var code = (await live.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        Factory.Services.GetRequiredService<SettablePresentations>().Set(SettablePresentations.At(deck, slide: 1));
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        return joined.GetProperty("current").Clone();
    }
}
