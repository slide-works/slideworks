using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Realtime;

// /hubs/deck: the realtime transport for IDeckEvents, over WebSockets only (Program.cs maps it). A client calls
// JoinDeck(deckId) and from then on receives that deck's events as "deckEvent" messages whose payload is the DeckEvent
// JSON (src/SlideWorks.Client/src/realtime/deckEvents.ts). It receives nothing for a deck it has not joined.
// LeaveDeck(deckId) stops that deck's events; a closed connection leaves every deck. Joining twice is harmless.
//
// Anyone may connect (K1): a viewer of a deck shared by link has no session. The connection is authenticated by the
// session cookie when there is one, and JoinDeck decides who may follow which deck, refusing an unknown deck and a
// forbidden one with the same HubException so the two can't be told apart. CrossSiteWriteGuard refuses a negotiate or a
// connect that carries another site's Origin, so a page elsewhere can't follow decks with a viewer's cookie.
[AllowAnonymous]
public sealed class DeckHub(IDeckAccess access, ICurrentActor actor) : Hub
{
    public const string PATH = "/hubs/deck";

    public const string EVENT_METHOD = "deckEvent";

    public const string DECK_NOT_FOUND = "Deck not found";

    public static string GroupName(string deckId) => $"deck:{deckId}";

    // The caller must be able to view the deck (IDeckAccess.CanViewAsync), so an anonymous viewer follows a deck shared by
    // link. Events carry only the deck id: a connection that joined before its access was revoked learns only that the
    // deck changed, and its re-read through the API is refused (K9).
    public async Task JoinDeck(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new HubException(DECK_NOT_FOUND);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(deckId));
    }

    public Task LeaveDeck(string deckId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(deckId));
}
