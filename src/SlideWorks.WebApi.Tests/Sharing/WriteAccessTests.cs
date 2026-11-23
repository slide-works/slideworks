using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// Read and write access (C4, C5, D7, D8, D12). Sam owns each deck; Alex has write or read through the sharing PUT's
// people list. A person with write edits in the browser and through their own agent and presents; only the owner changes
// sharing. At level private only the owner gets in, the list is kept, and the deck is not listed for the people on it.
[TestFixture]
[CancelAfter(60000)]
public class WriteAccessTests : ApiTestFixture
{
    [Test]
    public async Task EditSlide_WhenWrite_ShouldSucceed()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex));
        using var alex = ClientFor(TestUser.Alex);

        var edit = await alex.PutAsJsonAsync($"/api/decks/{deck}/look", new { draft = true });
        var page = await alex.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        edit.StatusCode.ShouldBe(HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
        page.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        page.GetProperty("access").GetString().ShouldBe("write");
        page.GetProperty("isOwner").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task EditSlide_WhenRead_ShouldReturn404()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "named", ("read", TestUser.Alex));
        using var alex = ClientFor(TestUser.Alex);

        var edit = await alex.PutAsJsonAsync($"/api/decks/{deck}/look", new { draft = true });
        var page = await alex.GetAsync($"/api/decks/{deck}");

        edit.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await edit.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Deck not found");
        page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AgentEdit_WhenPresenterHasWrite_ShouldSucceed()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        await using var mira = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira);
        var deck = await DeckSeed.CreateDeckAsync(sam);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex), ("read", TestUser.Mira));

        var added = await DeckSeed.AddQuoteAsync(alex, deck, "Edited by Alex's agent");
        var refused = await mira.CallToolAsync("add_slide", new { deck, layout = "quote", slots = new { quote = "No", attribution = "Mira" } });
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

        added.ValueKind.ShouldBe(JsonValueKind.Object);
        refused.IsError.ShouldBeTrue();
        refused.Json.GetString()!.ShouldContain("Deck not found");
        page.GetProperty("slides").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task ChangeSharing_WhenWrite_ShouldReturn404()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex));
        using var alex = ClientFor(TestUser.Alex);

        var put = await SharingApi.PutAsync(alex, deck, new { level = "link", people = Array.Empty<string>() });
        var read = await alex.GetAsync($"/api/decks/{deck}/sharing");
        var after = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        after.GetProperty("level").GetString().ShouldBe("named");
    }

    [Test]
    public async Task ChangeSharing_WhenWriteAgent_ShouldBeRefused()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex));

        var result = await alex.CallToolAsync("set_sharing", new { deck, level = "link" });
        var after = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        result.IsError.ShouldBeTrue();
        after.GetProperty("level").GetString().ShouldBe("named");
    }

    [Test]
    public async Task Present_WhenWrite_ShouldBeAllowed()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex), ("read", TestUser.Mira));
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        var writePlay = await alex.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");
        var readPlay = await mira.GetFromJsonAsync<JsonElement>($"/api/play/{deck}");

        writePlay.GetProperty("isOwner").GetBoolean().ShouldBeTrue();
        readPlay.GetProperty("isOwner").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task View_WhenPrivateAndWrite_ShouldReturn404()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "private", ("write", TestUser.Alex));
        using var alex = ClientFor(TestUser.Alex);

        var play = await alex.GetAsync($"/api/play/{deck}");
        var page = await alex.GetAsync($"/api/decks/{deck}");
        var owner = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        play.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        owner.GetProperty("people").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task View_WhenLinkAndWrite_ShouldEdit()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharePeopleAsync(deck, "link", ("write", TestUser.Alex), ("read", TestUser.Mira));
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);

        var writeEdit = await alex.PutAsJsonAsync($"/api/decks/{deck}/look", new { draft = true });
        var readEdit = await mira.PutAsJsonAsync($"/api/decks/{deck}/look", new { draft = true });
        var readPlay = await mira.GetAsync($"/api/play/{deck}");

        writeEdit.StatusCode.ShouldBe(HttpStatusCode.OK, await writeEdit.Content.ReadAsStringAsync());
        readEdit.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        readPlay.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task View_WhenPrivateThenNamedAgain_ShouldLetWritePersonEdit()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex));
        await SharePeopleAsync(deck, "private", ("write", TestUser.Alex));

        var whilePrivate = await alex.CallToolAsync("add_slide", new { deck, layout = "quote", slots = new { quote = "Hidden", attribution = "Alex" } });
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex));
        var again = await DeckSeed.AddQuoteAsync(alex, deck, "Back in");

        whilePrivate.IsError.ShouldBeTrue();
        again.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Test]
    public async Task ListDecks_WhenWriteShare_ShouldIncludeDeckWithAccessWrite()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var shared = await DeckSeed.CreateDeckAsync(sam, "Shared with write");
        var own = await DeckSeed.CreateDeckAsync(alex, "Alex's own");
        await SharePeopleAsync(shared, "named", ("write", TestUser.Alex));

        var (isError, result) = await alex.CallToolAsync("list_decks");
        var rows = result.GetProperty("decks").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString()!, row => row.GetProperty("access").GetString());
        var (_, samResult) = await sam.CallToolAsync("list_decks");

        isError.ShouldBeFalse(result.ToString());
        rows[shared].ShouldBe("write");
        rows[own].ShouldBe("owner");
        samResult.GetProperty("decks").EnumerateArray().Single(row => row.GetProperty("id").GetString() == shared).GetProperty("access").GetString().ShouldBe("owner");
    }

    [Test]
    public async Task SharedWithMe_WhenPrivate_ShouldHideDeckUntilNamedAgain()
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alexAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        await using var miraAgent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Mira);
        using var alex = ClientFor(TestUser.Alex);
        using var mira = ClientFor(TestUser.Mira);
        var deck = await DeckSeed.CreateDeckAsync(sam);

        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex), ("read", TestUser.Mira));
        var named = (await AccessInLibraryAsync(alex, deck), await AccessInLibraryAsync(mira, deck), await AccessInAgentListAsync(alexAgent, deck), await AccessInAgentListAsync(miraAgent, deck));
        await SharePeopleAsync(deck, "private", ("write", TestUser.Alex), ("read", TestUser.Mira));
        var paused = (await AccessInLibraryAsync(alex, deck), await AccessInLibraryAsync(mira, deck), await AccessInAgentListAsync(alexAgent, deck), await AccessInAgentListAsync(miraAgent, deck));
        await SharePeopleAsync(deck, "link", ("write", TestUser.Alex), ("read", TestUser.Mira));
        var link = (await AccessInLibraryAsync(alex, deck), await AccessInLibraryAsync(mira, deck), await AccessInAgentListAsync(alexAgent, deck), await AccessInAgentListAsync(miraAgent, deck));
        await SharePeopleAsync(deck, "named", ("write", TestUser.Alex), ("read", TestUser.Mira));
        var again = (await AccessInLibraryAsync(alex, deck), await AccessInLibraryAsync(mira, deck), await AccessInAgentListAsync(alexAgent, deck), await AccessInAgentListAsync(miraAgent, deck));

        named.ShouldBe(("write", "read", "write", null));
        paused.ShouldBe((null, null, null, null));
        link.ShouldBe(("write", "read", "write", null));
        again.ShouldBe(("write", "read", "write", null));
    }

    private async Task SharePeopleAsync(string deck, string level, params (string Access, TestUser Person)[] people)
    {
        var body = new { level, people = people.Select(p => new { email = p.Person.Email, access = p.Access }).ToArray() };
        var response = await SharingApi.PutAsync(Client, deck, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<string?> AccessInLibraryAsync(HttpClient person, string deck)
    {
        var library = await person.GetFromJsonAsync<JsonElement>("/api/decks");
        return library.GetProperty("sharedWithMe").EnumerateArray()
            .Where(row => row.GetProperty("id").GetString() == deck)
            .Select(row => row.GetProperty("access").GetString())
            .SingleOrDefault();
    }

    private static async Task<string?> AccessInAgentListAsync(McpTestClient agent, string deck)
    {
        var (_, result) = await agent.CallToolAsync("list_decks");
        return result.GetProperty("decks").EnumerateArray()
            .Where(row => row.GetProperty("id").GetString() == deck)
            .Select(row => row.GetProperty("access").GetString())
            .SingleOrDefault();
    }
}
