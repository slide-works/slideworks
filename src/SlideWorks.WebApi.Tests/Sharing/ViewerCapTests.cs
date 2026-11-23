using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// A deck names at most 50 people. The cap counts distinct emails after trimming and lower-casing, so repeats and the
// owner's own email don't use it up. The work is bounded too: a list of more than 200 entries is refused before any of
// it is looked at, and the walk stops at the 51st distinct email, so one request can't hold a core (SW-31 review round 1).
// The PUT and set_sharing share the rule.
[TestFixture]
public class ViewerCapTests : ApiTestFixture
{
    // These tests share 50 people at once, far past the default new-recipient limits (SW-794).
    protected override Dictionary<string, string?> ExtraConfig => new() { [SharingLimits.NEW_PER_HOUR_KEY] = "10000", [SharingLimits.NEW_PER_DAY_KEY] = "10000" };

    private const string TOO_MANY = "viewers: at most 50 emails";

    private const string PEOPLE_TOO_MANY = "people: at most 50 emails";

    private const int HUGE_LIST = 100_000;

    // Generous: the PUT stops reading the list at entry 201 and answers in a few milliseconds. The old quadratic walk
    // took 2.2 s for 20,000 emails, so 100,000 would take about a minute.
    private static readonly TimeSpan FAST = TimeSpan.FromMilliseconds(200);

    // /mcp parses the whole JSON-RPC body before any tool runs: on this host an ignored extra argument of 100,000
    // strings alone costs 180-630 ms, set_sharing or not. So this bound only proves set_sharing adds no quadratic walk on
    // top; bounding the body itself is the transport's job, not the tool's.
    private static readonly TimeSpan FAST_OVER_MCP = TimeSpan.FromSeconds(3);

    [Test]
    public async Task Put_WhenFiftyDistinctViewers_ShouldStoreThemAll()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var view = await SharingApi.PutOkAsync(Client, deck, "named", Emails(50));

        view.GetProperty("viewers").GetArrayLength().ShouldBe(50);
    }

    [Test]
    public async Task Put_WhenFiftyOneDistinctViewers_ShouldReturn400()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = Emails(51) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(TOO_MANY);
        var sharing = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        sharing.GetProperty("level").GetString().ShouldBe("private");
        sharing.GetProperty("viewers").GetArrayLength().ShouldBe(0);
    }

    // 50 people, each written three ways, plus the owner twice: 152 entries, 50 viewers.
    [Test]
    public async Task Put_WhenDuplicatesAndOwner_ShouldNotCountTowardsTheCap()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var distinct = Emails(50);
        var repeated = distinct
            .SelectMany(email => new[] { email, $"  {email.ToUpperInvariant()} ", $"{char.ToUpperInvariant(email[0])}{email[1..]}" })
            .Append(TestUser.Sam.Email)
            .Append(TestUser.Sam.Email.ToUpperInvariant())
            .ToArray();

        var view = await SharingApi.PutOkAsync(Client, deck, "named", repeated);

        view.GetProperty("viewers").EnumerateArray().Select(v => v.GetString()).ShouldBe(distinct.Order(StringComparer.Ordinal));
    }

    // More than 200 entries is refused before any is normalized, even when they are all the same person.
    [Test]
    public async Task Put_WhenMoreThanTwoHundredEntries_ShouldReturn400()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = Enumerable.Repeat(TestUser.Alex.Email, 201).ToArray() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(TOO_MANY);
    }

    [Test]
    public async Task Put_WhenHundredThousandViewers_ShouldReturn400Fast()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = Emails(51) });
        using var content = new StringContent(JsonSerializer.Serialize(new { level = "named", viewers = Emails(HUGE_LIST) }), Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        using var response = await Client.PutAsync($"/api/decks/{deck}/sharing", content);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(TOO_MANY);
        await TestContext.Out.WriteLineAsync($"PUT with {HUGE_LIST} viewers answered in {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Elapsed.ShouldBeLessThan(FAST);
    }

    [Test]
    public async Task Put_WhenMoreThanTwoHundredPeople_ShouldReturn400()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", people = Enumerable.Repeat(new { email = TestUser.Alex.Email, access = "read" }, 201).ToArray() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(PEOPLE_TOO_MANY);
    }

    [Test]
    public async Task Put_WhenHundredThousandPeople_ShouldReturn400Fast()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var people = Emails(HUGE_LIST).Select(email => new { email, access = "read" }).ToArray();
        using var content = new StringContent(JsonSerializer.Serialize(new { level = "named", people }), Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        using var response = await Client.PutAsync($"/api/decks/{deck}/sharing", content);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe(PEOPLE_TOO_MANY);
        await TestContext.Out.WriteLineAsync($"PUT with {HUGE_LIST} people answered in {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Elapsed.ShouldBeLessThan(FAST);
    }

    [Test]
    public async Task SetSharing_WhenFiftyOneDistinctViewers_ShouldBeAToolError()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var fifty = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = Emails(50).Append(TestUser.Sam.Email).ToArray() });
        var fiftyOne = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = Emails(51) });

        fifty.IsError.ShouldBeFalse(fifty.Json.ToString());
        fifty.Json.GetProperty("viewers").GetArrayLength().ShouldBe(50);
        fiftyOne.IsError.ShouldBeTrue();
        fiftyOne.Json.GetString().ShouldBe(TOO_MANY);
    }

    [Test]
    public async Task SetSharing_WhenHundredThousandViewersOnTheWire_ShouldBeAToolErrorFast()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", $$"""{"deck":"{{deck}}","level":"named","viewers":{{JsonSerializer.Serialize(Emails(51))}}}""");
        var arguments = $$"""{"deck":"{{deck}}","level":"named","viewers":{{JsonSerializer.Serialize(Emails(HUGE_LIST))}}}""";

        var stopwatch = Stopwatch.StartNew();
        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "set_sharing", arguments);
        stopwatch.Stop();

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe(TOO_MANY);
        await TestContext.Out.WriteLineAsync($"set_sharing with {HUGE_LIST} viewers answered in {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Elapsed.ShouldBeLessThan(FAST_OVER_MCP);
    }

    private static string[] Emails(int count) => Enumerable.Range(0, count).Select(i => $"viewer{i}@team.example").ToArray();
}
