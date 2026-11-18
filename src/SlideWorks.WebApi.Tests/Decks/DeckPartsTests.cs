using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// K19: a part is {name, short, caption, mark} on every view; create_deck and POST /api/decks take a string or an object
// per part, and a marked part is an unnumbered section that comes after every numbered one.
[TestFixture]
public class DeckPartsTests : ApiTestFixture
{
    [Test]
    public async Task CreateDeck_WhenAPartHasAMark_ShouldLeaveItOffTheTracker()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var parts = new object[] { "One", new { name = "Two", @short = "2nd", caption = "Middle" }, new { name = "Appendix", mark = "A" } };

        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", parts });

        isError.ShouldBeFalse(json.ToString());
        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{json.GetProperty("deck").GetString()}");
        var read = play.GetProperty("parts").EnumerateArray().ToList();
        read.Count.ShouldBe(3);
        read[1].GetProperty("short").GetString().ShouldBe("2nd");
        read[1].GetProperty("caption").GetString().ShouldBe("Middle");
        read[1].GetProperty("mark").ValueKind.ShouldBe(JsonValueKind.Null);
        read[2].GetProperty("name").GetString().ShouldBe("Appendix");
        read[2].GetProperty("short").GetString().ShouldBe("Appendix");
        read[2].GetProperty("mark").GetString().ShouldBe("A");
    }

    [Test]
    public async Task CreateDeck_WhenPartsAreStrings_ShouldStillWork()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Ship smaller", parts = new[] { "One", "Two" } });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var play = await Client.GetFromJsonAsync<JsonElement>($"/api/play/{id}");
        var parts = play.GetProperty("parts").EnumerateArray().ToList();
        parts.Select(p => p.GetProperty("name").GetString()).ShouldBe(["One", "Two"]);
        parts.Select(p => p.GetProperty("short").GetString()).ShouldBe(["One", "Two"]);
        parts.ShouldAllBe(p => p.GetProperty("caption").ValueKind == JsonValueKind.Null && p.GetProperty("mark").ValueKind == JsonValueKind.Null);
    }

    [Test]
    public async Task CreateDeck_WhenAMarkedPartComesBeforeANumberedOne_ShouldRefuse()
    {
        var error = await RefusalAsync(new object[] { "One", new { name = "Appendix", mark = "A" }, "Three" });

        error.ShouldContain("parts[1]: a part with a mark must come after every numbered part");
    }

    [Test]
    public async Task CreateDeck_WhenShortIs17_ShouldRefuse()
    {
        var error = await RefusalAsync(new object[] { "One", new { name = "Two", @short = new string('s', 17) } });

        error.ShouldContain("parts[1].short: at most 16 characters");
    }

    [Test]
    public async Task CreateDeck_WhenNineParts_ShouldRefuse()
    {
        var error = await RefusalAsync(Enumerable.Range(1, 9).Select(n => (object)$"Part {n}").ToArray());

        error.ShouldContain("parts: at most 8 items");
    }

    [TestCase("""{"name":"Two","colour":"red"}""", "parts[1].colour: unknown field")]
    [TestCase("""{"name":"Two","mark":"ABC"}""", "parts[1].mark: at most 2 characters")]
    [TestCase("""{"name":"Two","caption":"0123456789012345678901234"}""", "parts[1].caption: at most 24 characters")]
    [TestCase("""{"name":""}""", "parts[1]: required")]
    [TestCase("3", "parts[1]: must be a name or {name, short, caption, mark}")]
    public async Task CreateDeck_WhenAPartIsMalformed_ShouldRefuse(string part, string error)
    {
        var response = await Client.PostAsync("/api/decks", new StringContent($$"""{"title":"T","parts":["One",{{part}}]}""", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldStartWith(error);
    }

    [Test]
    public async Task CreateDeck_WhenAPartIsRefused_ShouldCreateNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = await CountDecksAsync();

        var (isError, _) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", parts = new object[] { "One", new { name = "Two", @short = new string('s', 17) } } });

        isError.ShouldBeTrue();
        (await CountDecksAsync()).ShouldBe(before);
    }

    private async Task<int> CountDecksAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Decks.CountAsync();
    }

    [Test]
    public async Task GetDeck_WhenPartsWereStoredAsStrings_ShouldReturnObjects()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE Decks SET PartsJson = '[\"Old one\",\"Old two\"]' WHERE Id = {0}", deck);
        }

        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });

        isError.ShouldBeFalse(json.ToString());
        var parts = json.GetProperty("parts").EnumerateArray().ToList();
        parts.Select(p => p.GetProperty("name").GetString()).ShouldBe(["Old one", "Old two"]);
        parts.Select(p => p.GetProperty("short").GetString()).ShouldBe(["Old one", "Old two"]);
        parts[0].GetProperty("mark").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task CreateDeck_WhenPartsAreObjectsOnTheWire_ShouldReturnAnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var created = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "create_deck", """{"title":"Ship smaller","parts":["One",{"name":"Two","short":"2","mark":null}]}""");

        (created.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(created.GetRawText());
        var deck = created.GetProperty("structuredContent").GetProperty("deck").GetString();
        var got = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck", $$"""{"deck":"{{deck}}"}""");
        var parts = got.GetProperty("structuredContent").GetProperty("parts").EnumerateArray().ToList();
        parts[1].EnumerateObject().Select(p => p.Name).ShouldBe(["name", "short", "caption", "mark"], ignoreOrder: true);
        parts[1].GetProperty("short").GetString().ShouldBe("2");
    }

    [Test]
    public async Task Presenter_WhenTheDeckHasParts_ShouldCarryThemAsObjects()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, parts: ["One", "Two"]);

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/presenter");

        var parts = view.GetProperty("parts").EnumerateArray().ToList();
        parts.Select(p => p.GetProperty("name").GetString()).ShouldBe(["One", "Two"]);
        parts[0].GetProperty("short").GetString().ShouldBe("One");
    }

    private async Task<string> RefusalAsync(object[] parts)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var (isError, json) = await agent.CallToolAsync("create_deck", new { title = "Ship smaller", parts });

        isError.ShouldBeTrue();
        return json.ToString();
    }
}
