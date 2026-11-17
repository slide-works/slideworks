using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime.Presenting;

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
//
// Presenting (SW-4, P2): JoinPresentation, UpdatePresentation, SetTimer and LeavePresentation. The hub only checks access
// and delegates to IPresentations with the caller's connection id; the registry owns the present:{deckId} groups and
// sends "presentationState". A closed connection leaves every presentation.
[AllowAnonymous]
public sealed class DeckHub(IDeckAccess access, ICurrentActor actor, IPresentations presentations) : Hub
{
    public const string PATH = "/hubs/deck";

    public const string EVENT_METHOD = "deckEvent";

    public const string DECK_NOT_FOUND = "Deck not found";

    // The server -> client message carrying a PresentationState (P1) to every connection in present:{deckId}.
    public const string PRESENTATION_STATE_METHOD = "presentationState";

    // UpdatePresentation's and SetTimer's refusal when the connection hasn't joined the deck's presentation (P2).
    public const string NOT_PRESENTING = "Not presenting";

    public static string GroupName(string deckId) => $"deck:{deckId}";

    // The group of a deck's presentation: its presenter and audience windows and phones (P2).
    public static string PresentationGroupName(string deckId) => $"present:{deckId}";

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

    // Only the deck's owner, signed in as a person, may join (D3, IDeckAccess.CanPresentAsync). Anyone else gets the same
    // refusal as JoinDeck, before anything changes.
    public async Task<PresentationJoined> JoinPresentation(
        string deckId,
        string clientId,
        PresentationRole role,
        string label,
        string timeZone,
        PresentationResume? resume)
    {
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new HubException(DECK_NOT_FOUND);
        }
        return await presentations.JoinAsync(Context.ConnectionId, deckId, clientId, role, label, timeZone, resume);
    }

    // Only a connection that joined the presentation may move it; the registry refuses the rest with NOT_PRESENTING.
    public Task<PresentationState> UpdatePresentation(string deckId, PresentationUpdate update)
        => presentations.UpdateAsync(Context.ConnectionId, deckId, update);

    public Task<PresentationState> SetTimer(string deckId, TimerCommand command)
        => presentations.SetTimerAsync(Context.ConnectionId, deckId, command);

    public Task LeavePresentation(string deckId) => presentations.LeaveAsync(Context.ConnectionId, deckId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presentations.LeaveAllAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
