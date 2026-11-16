using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Realtime;

// /hubs/deck: the realtime transport for IDeckEvents, over WebSockets only (Program.cs maps it). A client calls
// JoinDeck(deckId) and from then on receives that deck's events as "deckEvent" messages whose payload is the DeckEvent
// JSON (src/SlideWorks.Client/src/realtime/deckEvents.ts). It receives nothing for a deck it has not joined.
// LeaveDeck(deckId) stops that deck's events; a closed connection leaves every deck. Joining twice is harmless.
//
// Who may connect is BearerTokenAuthMiddleware, which guards /hubs as it guards /api. JoinDeck has no per-deck check yet
// because there are no decks or accounts yet: when SW-1 adds them, JoinDeck checks the caller may view the deck and
// refuses one they can't with a HubException, the same as an unknown deck.
public sealed class DeckHub : Hub
{
    public const string PATH = "/hubs/deck";

    public const string EVENT_METHOD = "deckEvent";

    public static string GroupName(Guid deckId) => $"deck:{deckId}";

    public Task JoinDeck(Guid deckId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(deckId));

    public Task LeaveDeck(Guid deckId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(deckId));
}
