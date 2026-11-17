using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure.RateLimits;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Realtime;

// /hubs/deck: the realtime transport for IDeckEvents, over WebSockets, with Server-Sent Events and long polling as the fallbacks guests' networks need (D7; Program.cs maps them). A client calls
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
//
// Audience (SW-5, J4): a guest (anonymous, with the sw_guest cookie) calls JoinAudience(code) and receives "audienceEvent"
// messages for that deck's session; the owner calls WatchAudience(deckId) for the joined count. The hub only checks and
// delegates: IAudienceSessions finds the session and its phase, IAudienceRoster counts guests, IAudienceRelay composes
// the state, and IAudienceEvents carries what goes out. A closed connection leaves the roster.
[AllowAnonymous]
public sealed class DeckHub(
    IDeckAccess access,
    IPresentations presentations,
    IAudienceSessions sessions,
    IAudienceRelay relay,
    IAudienceRoster roster,
    IJoinLookupLimiter limiter,
    IAudienceEvents audienceEvents) : Hub
{
    // The caller is the hub connection's own principal, not the request's: under long polling a hub call runs after the
    // request that opened it has finished, so IHttpContextAccessor is empty by then.
    private ICurrentActor Actor => new PrincipalActor(Context.User ?? new ClaimsPrincipal());

    public const string PATH = "/hubs/deck";

    public const string EVENT_METHOD = "deckEvent";

    public const string DECK_NOT_FOUND = "Deck not found";

    // The server -> client message carrying a PresentationState (P1) to every connection in present:{deckId}.
    public const string PRESENTATION_STATE_METHOD = "presentationState";

    // UpdatePresentation's and SetTimer's refusal when the connection hasn't joined the deck's presentation (P2).
    public const string NOT_PRESENTING = "Not presenting";

    // The server -> client message carrying an AudienceEvent (J4).
    public const string AUDIENCE_EVENT_METHOD = "audienceEvent";

    // JoinAudience's refusals (J4). The guest-facing ones are exactly these strings.
    public const string NO_GUEST = "no_guest";

    public const string NO_SESSION = "no_session";

    public const string TOO_MANY_ATTEMPTS = "too_many_attempts";

    public static string GroupName(string deckId) => $"deck:{deckId}";

    // The group of a deck's presentation: its presenter and audience windows and phones (P2).
    public static string PresentationGroupName(string deckId) => $"present:{deckId}";

    // The caller must be able to view the deck (IDeckAccess.CanViewAsync), so an anonymous viewer follows a deck shared by
    // link. Events carry only the deck id: a connection that joined before its access was revoked learns only that the
    // deck changed, and its re-read through the API is refused (K9).
    public async Task JoinDeck(string deckId)
    {
        if (!await access.CanViewAsync(deckId, Actor))
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
        if (!await access.CanPresentAsync(deckId, Actor))
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

    // The guests of a deck's session: audienceState and audienceEnded (J4).
    public static string AudienceGroupName(string deckId) => $"audience:{deckId}";

    // The owner's view of the audience: joinedCount (J4).
    public static string AudienceWatchGroupName(string deckId) => $"audience-watch:{deckId}";

    // A guest follows a deck by its join code. Needs the sw_guest cookie. A code that finds no session counts as a miss
    // toward the rate limit and is refused as NO_SESSION; a caller out of misses is refused before the lookup. The guest
    // joins the group and the roster in every phase (a lobby guest waits for the state that makes it live, and is counted
    // when it does). Returns the current state.
    public async Task<AudienceState> JoinAudience(string code)
    {
        var guestId = GuestCookie.IdOf(Context.GetHttpContext()) ?? throw new HubException(NO_GUEST);
        var caller = CallerKey.For(Context.GetHttpContext()?.Connection.RemoteIpAddress);
        if (!limiter.Allow(caller))
        {
            throw new HubException(TOO_MANY_ATTEMPTS);
        }
        var found = await sessions.FindAsync(code);
        if (found == null)
        {
            limiter.Miss(caller);
            throw new HubException(NO_SESSION);
        }
        limiter.Hit(caller);
        // Everything that can refuse comes before anything changes: a refused call leaves no group, roster or count behind.
        _ = await relay.CurrentAsync(found.DeckId) ?? throw new HubException(NO_SESSION);
        await Groups.AddToGroupAsync(Context.ConnectionId, AudienceGroupName(found.DeckId));
        // Read again now the guest is in the group: a state published between the first read and the join would otherwise
        // be missed by this guest. A state that arrives after this read reaches it through the group.
        var state = await relay.CurrentAsync(found.DeckId);
        if (state == null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AudienceGroupName(found.DeckId));
            throw new HubException(NO_SESSION);
        }
        // Every phase goes on the roster (D22): a lobby guest is counted when the session opens. Only a live session's count
        // is told to the owner.
        var count = await roster.JoinAsync(found.DeckId, found.SessionId, guestId, Context.ConnectionId);
        if (found.Phase == AudiencePhase.Live)
        {
            await audienceEvents.PublishAsync(new JoinedCount(found.DeckId, count));
        }
        return state;
    }

    // Stops the code's events and takes the connection off the roster. An unknown code changes nothing.
    public async Task LeaveAudience(string code)
    {
        var found = await sessions.FindAsync(code);
        if (found == null)
        {
            return;
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AudienceGroupName(found.DeckId));
        await LeaveRosterAsync();
    }

    // Only the deck's owner, signed in as a person, may watch the joined count: anyone else gets the deck-not-found
    // refusal, before anything changes.
    public async Task WatchAudience(string deckId)
    {
        if (!await access.CanPresentAsync(deckId, Actor))
        {
            throw new HubException(DECK_NOT_FOUND);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, AudienceWatchGroupName(deckId));
    }

    private async Task LeaveRosterAsync()
    {
        var left = await roster.LeaveAsync(Context.ConnectionId);
        if (left != null)
        {
            await audienceEvents.PublishAsync(new JoinedCount(left.DeckId, left.Count));
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await presentations.LeaveAllAsync(Context.ConnectionId);
        await LeaveRosterAsync();
        await base.OnDisconnectedAsync(exception);
    }
}
