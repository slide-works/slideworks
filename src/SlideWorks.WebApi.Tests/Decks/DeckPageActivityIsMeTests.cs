using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// SW-859 (D37): each activity row on the deck page carries isMe, true only for a person's entry made by the person looking
// at the page. The feed reads "You" from it, so a writer's edit reads by the writer's name on the owner's page.
[TestFixture]
public class DeckPageActivityIsMeTests : ApiTestFixture
{
    [Test]
    public async Task Get_WhenOwnerEditedInTheBrowser_ShouldMarkTheEntryAsMine()
    {
        var deck = await CreateDeckAsync(Client);

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];

        entry.GetProperty("actor").GetString().ShouldBe("Sam Rivera");
        entry.GetProperty("actorKind").GetString().ShouldBe("person");
        entry.GetProperty("isMe").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenWriterEditedAndOwnerLooks_ShouldNotMarkTheEntryAsMineAndKeepTheWritersName()
    {
        var deck = await CreateDeckSharedWithWriterAsync();
        await PutPaletteAsync(ClientFor(TestUser.Alex), deck);

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];

        entry.GetProperty("actor").GetString().ShouldBe("Alex Chen");
        entry.GetProperty("summary").GetString().ShouldBe("changed the palette to Ember");
        entry.GetProperty("isMe").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenWriterEditedAndWriterLooks_ShouldMarkTheEntryAsMine()
    {
        var deck = await CreateDeckSharedWithWriterAsync();
        var writer = ClientFor(TestUser.Alex);
        await PutPaletteAsync(writer, deck);

        var activity = await DeckSeed.ActivityAsync(writer, deck);

        activity[0].GetProperty("isMe").GetBoolean().ShouldBeTrue();
        activity.Skip(1).ShouldAllBe(a => !a.GetProperty("isMe").GetBoolean());
    }

    [Test]
    public async Task Get_WhenEntryHasNoStoredActorId_ShouldShowTheNameNotYou()
    {
        var deck = await CreateDeckAsync(Client);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.ActivityEntries.Where(e => e.DeckId == deck).ExecuteUpdateAsync(set => set.SetProperty(e => e.ActorUserId, (Guid?)null));
        }

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];

        entry.GetProperty("actor").GetString().ShouldBe("Sam Rivera");
        entry.GetProperty("isMe").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenAgentMadeTheEntry_ShouldNotMarkItAsMine()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];

        entry.GetProperty("actorKind").GetString().ShouldBe("agent");
        entry.GetProperty("isMe").GetBoolean().ShouldBeFalse();
    }

    private async Task<string> CreateDeckSharedWithWriterAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await agent.CallToolAsync("set_sharing", new { deck, level = "named", person = TestUser.Alex.Email, access = "write" });
        return deck;
    }

    private static async Task<string> CreateDeckAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/decks", new { title = "Mine" });
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task PutPaletteAsync(HttpClient client, string deck)
    {
        var response = await client.PutAsJsonAsync($"/api/decks/{deck}/look", new { palette = "ember" });
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
