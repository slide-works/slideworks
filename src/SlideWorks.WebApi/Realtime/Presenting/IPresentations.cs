using SlideWorks.WebApi.Features.Presenting.Views;

namespace SlideWorks.WebApi.Realtime.Presenting;

// The presentations registry (P2, P4, P7, P10): in memory, one singleton, single process (D9), like IDeckEvents. DeckHub
// checks access and delegates here with the caller's connection id; the registry owns the present:{deckId} groups and
// sends "presentationState" through IHubContext<DeckHub>. A refused call has no side effects.
public interface IPresentations
{
    // P2 JoinPresentation. The caller has passed IDeckAccess.CanPresentAsync. Creates the state from resume when the deck
    // has none, replaces an entry with the same clientId, adds the connection to the group and broadcasts.
    Task<PresentationJoined> JoinAsync(
        string connectionId,
        string deckId,
        string clientId,
        PresentationRole role,
        string label,
        string timeZone,
        PresentationResume? resume);

    // P2 UpdatePresentation. HubException(DeckHub.NOT_PRESENTING) unless this connection joined the deck's presentation.
    Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update);

    // P2 SetTimer. HubException(DeckHub.NOT_PRESENTING) unless this connection joined the deck's presentation.
    Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command);

    // P2 LeavePresentation: removes the entry this connection holds, if it still holds it.
    Task LeaveAsync(string connectionId, string deckId);

    // A closed connection leaves every presentation (DeckHub.OnDisconnectedAsync).
    Task LeaveAllAsync(string connectionId);

    // P4: the owner's decks with at least one presenter or audience connection, newest first.
    Task<LivePresentationsView> ListLiveAsync(Guid ownerId);

    // P7: the deck's talk length changed; sends a new state when the deck has one.
    Task TalkLengthChangedAsync(string deckId, int? minutes);

    // P10: the deck's current state, or null when there is none. No side effects; never revives a dropped state.
    Task<PresentationState?> GetAsync(string deckId);

    // P10: the existing state, or a new epoch at slide 1, step 0, timer notStarted, with no connections. Idempotent. No
    // access check: the caller checks IDeckAccess.CanPresentAsync first.
    Task<PresentationState> StartAsync(string deckId);
}
