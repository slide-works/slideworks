using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// P7 through PUT /api/decks/{id}/talk-length: the owner sets 1 to 600 minutes or clears it with null, and gets 204 with one
// activity entry, one DeckChanged and one TalkLengthChangedAsync. A refused call changes nothing and sends nothing.
[TestFixture]
public class TalkLengthTests : ApiTestFixture
{
    private readonly TalkLengthCalls _presentations = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IPresentations>(_presentations);

    [Test]
    public async Task Put_WhenOwnerSets30_ShouldReturn204RecordOneEntryAndPublishDeckChanged()
    {
        var deck = await CreateDeckAsync();
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());
        var before = await PageAsync(deck);

        var response = await PutAsync(deck, """{ "minutes": 30 }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await received.Reader.ReadAsync()).ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        received.Reader.TryRead(out _).ShouldBeFalse();
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64() + 1);
        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("set the talk length to 30 min");
        entry.GetProperty("via").GetString().ShouldBe("web");
        entry.GetProperty("tool").ValueKind.ShouldBe(JsonValueKind.Null);
        (await TalkMinutesAsync(deck)).ShouldBe(30);
        _presentations.For(deck).ShouldBe([30]);
    }

    [Test]
    public async Task Put_WhenNull_ShouldClearItAndRecordIt()
    {
        var deck = await CreateDeckAsync();
        await PutAsync(deck, """{ "minutes": 45 }""");

        var response = await PutAsync(deck, """{ "minutes": null }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TalkMinutesAsync(deck)).ShouldBeNull();
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("cleared the talk length");
        _presentations.For(deck).ShouldBe([45, (int?)null]);
    }

    [TestCase("""{ "minutes": 1 }""", 1)]
    [TestCase("""{ "minutes": 600 }""", 600)]
    public async Task Put_WhenOnTheEdgeOfTheRange_ShouldAcceptIt(string body, int minutes)
    {
        var deck = await CreateDeckAsync();

        (await PutAsync(deck, body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await TalkMinutesAsync(deck)).ShouldBe(minutes);
    }

    [TestCase("""{ "minutes": 0 }""")]
    [TestCase("""{ "minutes": 601 }""")]
    [TestCase("""{ "minutes": -5 }""")]
    [TestCase("""{ "minutes": 30.5 }""")]
    [TestCase("""{ "minutes": "30" }""")]
    [TestCase("""{ }""")]
    [TestCase("""{ "minutes": 30, "extra": true }""")]
    [TestCase("""{ "length": 30 }""")]
    [TestCase("""[30]""")]
    public async Task Put_WhenRefused_ShouldReturn400AndChangeNothing(string body)
    {
        var deck = await CreateDeckAsync();
        await PutAsync(deck, """{ "minutes": 20 }""");
        var before = await PageAsync(deck);
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        var response = await PutAsync(deck, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldNotBeNullOrEmpty();
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(before.GetProperty("activity").GetArrayLength());
        (await TalkMinutesAsync(deck)).ShouldBe(20);
        received.Reader.TryRead(out _).ShouldBeFalse();
        _presentations.For(deck).ShouldBe([20]);
    }

    // The range check runs after the owner check (D16), so a non-owner gets 404 whether the value is in range or not.
    [TestCase("""{ "minutes": 30 }""")]
    [TestCase("""{ "minutes": 0 }""")]
    [TestCase("""{ "minutes": 601 }""")]
    public async Task Put_WhenNotTheOwner_ShouldReturn404AndChangeNothing(string body)
    {
        var deck = await CreateDeckAsync();
        using var alex = ClientFor(TestUser.Alex);
        var before = await PageAsync(deck);
        var received = Channel.CreateUnbounded<DeckEvent>();
        using var subscription = Factory.Services.GetRequiredService<IDeckEvents>().Subscribe(deck, e => received.Writer.WriteAsync(e).AsTask());

        var response = await alex.PutAsync($"/api/decks/{deck}/talk-length", Json(body));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe("Deck not found");
        var after = await PageAsync(deck);
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("activity").GetArrayLength().ShouldBe(1);
        (await TalkMinutesAsync(deck)).ShouldBeNull();
        received.Reader.TryRead(out _).ShouldBeFalse();
        _presentations.For(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task Put_WhenTheDeckDoesNotExist_ShouldReturn404()
    {
        var response = await PutAsync("no-such-deck", """{ "minutes": 30 }""");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        _presentations.For("no-such-deck").ShouldBeEmpty();
    }

    private async Task<string> CreateDeckAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title = "Talk length" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PutAsync(string deck, string body) => Client.PutAsync($"/api/decks/{deck}/talk-length", Json(body));

    private Task<JsonElement> PageAsync(string deck) => Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}");

    // The talk length as get_deck shows it to the agent that owns the deck.
    private async Task<int?> TalkMinutesAsync(string deck)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (isError, json) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(json.ToString());
        var minutes = json.GetProperty("talk_minutes");
        return minutes.ValueKind == JsonValueKind.Null ? null : minutes.GetInt32();
    }
}
