namespace SlideWorks.WebApi.Realtime;

// Who is connected to a live session (J4). The count is the number of distinct guest ids with an open connection, and
// there is no cap (D7). PeakJoined is its maximum.
public interface IAudienceRoster
{
    // A guest's connection joined the session. Returns the count after it.
    Task<int> JoinAsync(string deckId, string sessionId, string guestId, string connectionId);

    // A connection closed or left, whichever session it was in. Null when the connection was on no roster.
    Task<RosterLeave?> LeaveAsync(string connectionId);

    Task<int> CountAsync(string sessionId);
}
