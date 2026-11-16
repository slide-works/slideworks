using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// /hubs/deck end to end: an event published on IDeckEvents reaches, over a real WebSocket, the connections that joined
// its deck and no others.
[TestFixture]
[CancelAfter(30000)]
public class DeckHubTests : ApiTestFixture
{
    private readonly List<HubReceiver> _receivers = [];

    private IDeckEvents Bus => Factory.Services.GetRequiredService<IDeckEvents>();

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
    public async Task Publish_WhenEventOnBus_ShouldReachConnectionsThatJoinedThatDeckOnly()
    {
        var (deckA, deckB) = (TestDeckIds.New(), TestDeckIds.New());
        var onA = await JoinedAsync(deckA);
        var onB = await JoinedAsync(deckB);
        var notJoined = await ConnectAsync();

        await Bus.PublishAsync(new DeckChanged(deckA));
        await Bus.PublishAsync(new DeckChanged(deckB));

        DeckIdOf(await onA.NextAsync()).ShouldBe(deckA);
        DeckIdOf(await onB.NextAsync()).ShouldBe(deckB);
        // Joining now and receiving the next event proves nothing earlier reached it.
        await notJoined.Connection.InvokeAsync("JoinDeck", deckB);
        await Bus.PublishAsync(new DeckChanged(deckB));
        DeckIdOf(await notJoined.NextAsync()).ShouldBe(deckB);
    }

    [Test]
    public async Task Publish_WhenEventOnBus_ShouldSendTheTypeDiscriminatedJson()
    {
        var deck = TestDeckIds.New();
        var receiver = await JoinedAsync(deck);

        await Bus.PublishAsync(new DeckChanged(deck));

        var received = await receiver.NextAsync();
        received.GetProperty("type").GetString().ShouldBe("deckChanged");
        received.GetProperty("deckId").GetString()!.ShouldBe(deck);
    }

    [Test]
    public async Task LeaveDeck_WhenCalled_ShouldStopThatDecksEvents()
    {
        var (left, kept) = (TestDeckIds.New(), TestDeckIds.New());
        var leaver = await JoinedAsync(left, kept);
        await leaver.Connection.InvokeAsync("LeaveDeck", left);

        await Bus.PublishAsync(new DeckChanged(left));
        await Bus.PublishAsync(new DeckChanged(kept));

        DeckIdOf(await leaver.NextAsync()).ShouldBe(kept);
    }

    // Anyone may connect (K1): a viewer of a link-shared deck has no session. JoinDeck decides, and for now it refuses
    // an anonymous caller exactly as it would an unknown deck.
    [Test]
    public async Task DeckHub_WhenAnonymousJoins_ShouldThrowDeckNotFound()
    {
        var anonymous = await HubReceiver.StartAsync(Factory, TestUser.Anonymous);
        _receivers.Add(anonymous);

        var refused = await Should.ThrowAsync<HubException>(() => anonymous.Connection.InvokeAsync("JoinDeck", TestDeckIds.New()));

        refused.Message.ShouldContain(DeckHub.DECK_NOT_FOUND);
    }

    // The anonymous join is refused by JoinDeck, not by the connection: a signed-in connection still joins.
    [Test]
    public async Task Connect_WhenSignedInAsAnotherPerson_ShouldJoinAndReceive()
    {
        var deck = TestDeckIds.New();
        var alex = await HubReceiver.StartAsync(Factory, TestUser.Alex);
        _receivers.Add(alex);
        await alex.Connection.InvokeAsync("JoinDeck", deck);

        await Bus.PublishAsync(new DeckChanged(deck));

        DeckIdOf(await alex.NextAsync()).ShouldBe(deck);
    }

    private async Task<HubReceiver> ConnectAsync()
    {
        var receiver = await HubReceiver.StartAsync(Factory, SignedInAs);
        _receivers.Add(receiver);
        return receiver;
    }

    private async Task<HubReceiver> JoinedAsync(params string[] decks)
    {
        var receiver = await ConnectAsync();
        foreach (var deck in decks)
        {
            await receiver.Connection.InvokeAsync("JoinDeck", deck);
        }
        return receiver;
    }

    private static string DeckIdOf(JsonElement deckEvent) => deckEvent.GetProperty("deckId").GetString()!;
}
