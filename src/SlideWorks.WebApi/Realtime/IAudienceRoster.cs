namespace SlideWorks.WebApi.Realtime;

// Who is connected to a deck's audience (J4, D22). Every audience connection is tracked from JoinAudience in every phase,
// so guests waiting in the lobby are already on the roster when the session opens. The count is the number of distinct
// guest ids with an open connection on the deck, and there is no cap (D7). PeakJoined is its maximum while a session is
// open.
public interface IAudienceRoster
{
    // A guest's connection joined the deck's audience. sessionId is the open session, or null in the lobby and ended
    // phases. Returns the number of distinct guests on the deck after it.
    Task<int> JoinAsync(string deckId, string? sessionId, string guestId, string connectionId);

    // A connection closed or left, whichever deck it was on. Null when the connection was on no roster, or the deck had no open
    // session to count it in.
    Task<RosterLeave?> LeaveAsync(string connectionId);

    // The distinct guests on the open session's deck, or 0 for a session that is not the deck's open one.
    Task<int> CountAsync(string sessionId);

    // Go live opened a session (D22): the guests already connected are counted, recorded as joined and published as the
    // joined count.
    Task OpenedAsync(string deckId, string sessionId);

    // The session ended: the guests stay connected, but nobody is counted until a session opens again. The session id is
    // remembered even when the roster has no entry for the deck, so a join in flight at the End can't reopen it.
    Task ClosedAsync(string deckId, string sessionId);
}
