using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Realtime;

// /hubs/deck: the realtime transport for IDeckEvents, over WebSockets only (Program.cs maps it). A client calls
// JoinDeck(deckId) and from then on receives that deck's events as "deckEvent" messages whose payload is the DeckEvent
// JSON (src/SlideWorks.Client/src/realtime/deckEvents.ts). It receives nothing for a deck it has not joined.
// LeaveDeck(deckId) stops that deck's events; a closed connection leaves every deck. Joining twice is harmless.
//
// Anyone may connect (K1): a viewer of a deck shared by link has no session. The connection is authenticated by the
// session cookie when there is one, and JoinDeck decides who may follow which deck, refusing an unknown deck and a
// forbidden one with the same HubException so the two can't be told apart.
[AllowAnonymous]
public sealed class DeckHub : Hub
{
    public const string PATH = "/hubs/deck";

    public const string EVENT_METHOD = "deckEvent";

    public const string DECK_NOT_FOUND = "Deck not found";

    public static string GroupName(string deckId) => $"deck:{deckId}";

    // TODO: check the caller may view the deck through IDeckAccess, which lets anonymous viewers follow a deck shared by
    // link. Until then only a signed-in person may join, and an anonymous caller is refused as if the deck didn't exist.
    public Task JoinDeck(string deckId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(deckId));

    public Task LeaveDeck(string deckId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(deckId));
}
