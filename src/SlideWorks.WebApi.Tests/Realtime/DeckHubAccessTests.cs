using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// Who may follow a deck over /hubs/deck (K9): JoinDeck lets in exactly those IDeckAccess lets view it, and refuses an
// unknown deck and a forbidden one with the same HubException. A negotiate or connect from another site's Origin is
// refused before any of that. Sam owns every deck here.
[TestFixture]
[CancelAfter(30000)]
public class DeckHubAccessTests : ApiTestFixture
{
    private const string FOREIGN_ORIGIN = "https://elsewhere.example";

    private readonly List<HubReceiver> _receivers = [];

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var receiver in _receivers)
        {
            await receiver.DisposeAsync();
        }
        _receivers.Clear();
    }

    [Test]
    public async Task JoinDeck_WhenForbidden_ShouldThrowSameAsUnknown()
    {
        var privateDeck = await SharingApi.CreateDeckAsync(Client);
        var namedDeck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, namedDeck, "named", TestUser.Mira.Email);
        var alex = await ConnectAsync(TestUser.Alex);
        var anonymous = await ConnectAsync(TestUser.Anonymous);

        var unknown = await RefusalAsync(alex, "nOsUcHdEcK00");
        var refusals = new[]
        {
            await RefusalAsync(alex, privateDeck),
            await RefusalAsync(alex, namedDeck),
            await RefusalAsync(anonymous, privateDeck),
            await RefusalAsync(anonymous, namedDeck),
            await RefusalAsync(anonymous, "nOsUcHdEcK00"),
        };

        unknown.ShouldContain(DeckHub.DECK_NOT_FOUND, Case.Sensitive);
        refusals.ShouldAllBe(message => message == unknown);
    }

    [Test]
    public async Task JoinDeck_WhenLinkDeckAnonymous_ShouldReceiveEvents()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SharingApi.PutOkAsync(Client, deck, "link");
        var anonymous = await ConnectAsync(TestUser.Anonymous);
        await anonymous.Connection.InvokeAsync("JoinDeck", deck);

        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches beat big plans.");

        DeckIdOf(await anonymous.NextAsync()).ShouldBe(deck);
    }

    // A named viewer follows the deck, a pending one too (SW-54 D2); the owner always does.
    [Test]
    public async Task JoinDeck_WhenNamedViewerOrOwner_ShouldReceiveEvents()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var pending = TestUser.NewPending();
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email, pending.Email);
        var joiners = new[] { await ConnectAsync(TestUser.Alex), await ConnectAsync(pending), await ConnectAsync(TestUser.Sam) };
        foreach (var joiner in joiners)
        {
            await joiner.Connection.InvokeAsync("JoinDeck", deck);
        }

        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email, pending.Email, TestUser.Mira.Email);

        foreach (var joiner in joiners)
        {
            DeckIdOf(await joiner.NextAsync()).ShouldBe(deck);
        }
    }

    // Accepted for SW-1 (K9, SW-16 D2): a connection that joined before its access was revoked stays in the group, but
    // the event carries only the deck id, and the re-read it prompts is refused.
    [Test]
    public async Task JoinedViewer_WhenRevoked_ShouldReceiveOnlyDeckIdAndApiRefuses()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await SharingApi.PutOkAsync(Client, deck, "link");
        var anonymous = await ConnectAsync(TestUser.Anonymous);
        await anonymous.Connection.InvokeAsync("JoinDeck", deck);
        using var viewer = ClientFor(TestUser.Anonymous);

        await SharingApi.PutOkAsync(Client, deck, "private");

        var received = await anonymous.NextAsync();
        received.EnumerateObject().Select(p => p.Name).ShouldBe(["type", "deckId"], ignoreOrder: true);
        received.GetProperty("type").GetString().ShouldBe("deckChanged");
        DeckIdOf(received).ShouldBe(deck);
        var reread = await viewer.GetAsync($"/api/play/{deck}");
        reread.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var refused = await RefusalAsync(await ConnectAsync(TestUser.Anonymous), deck);
        refused.ShouldContain(DeckHub.DECK_NOT_FOUND, Case.Sensitive);
    }

    // Another site's page could otherwise follow decks with a viewer's cookie. The expected origin is the one the
    // cross-site write guard uses: the configured public URL.
    [Test]
    public async Task Negotiate_WhenForeignOrigin_ShouldReturn403()
    {
        using var client = Factory.CreateClient();
        using var foreign = NegotiateRequest(FOREIGN_ORIGIN);
        using var own = NegotiateRequest(TestAppFactory.PUBLIC_URL);
        using var none = NegotiateRequest(origin: null);

        var foreignResponse = await client.SendAsync(foreign);
        var ownResponse = await client.SendAsync(own);
        var noneResponse = await client.SendAsync(none);

        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await foreignResponse.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Cross-site request refused");
        ownResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        noneResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Connect_WhenForeignOrigin_ShouldBeRefused()
    {
        var hub = new UriBuilder(Factory.Server.BaseAddress) { Scheme = "ws", Path = DeckHub.PATH }.Uri;

        using var own = await ConnectWebSocketAsync(hub, TestAppFactory.PUBLIC_URL);
        var refused = await Should.ThrowAsync<Exception>(() => ConnectWebSocketAsync(hub, FOREIGN_ORIGIN));

        own.State.ShouldBe(WebSocketState.Open);
        refused.Message.ShouldContain("403", Case.Sensitive);
    }

    private static HttpRequestMessage NegotiateRequest(string? origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{DeckHub.PATH}/negotiate?negotiateVersion=1");
        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
        }
        return request;
    }

    private async Task<WebSocket> ConnectWebSocketAsync(Uri hub, string origin)
    {
        var client = Factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Origin = origin;
        return await client.ConnectAsync(hub, CancellationToken.None);
    }

    private async Task<HubReceiver> ConnectAsync(TestUser user)
    {
        var receiver = await HubReceiver.StartAsync(Factory, user);
        _receivers.Add(receiver);
        return receiver;
    }

    private static async Task<string> RefusalAsync(HubReceiver receiver, string deck)
        => (await Should.ThrowAsync<HubException>(() => receiver.Connection.InvokeAsync("JoinDeck", deck))).Message;

    private static string DeckIdOf(JsonElement deckEvent) => deckEvent.GetProperty("deckId").GetString()!;
}
