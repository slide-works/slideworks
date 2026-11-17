namespace SlideWorks.WebApi.Realtime;

// Carries the presenter's position (SW-4's presentation state) to the audience (J4). The only source of that position
// for guests. CurrentAsync composes the state a guest follows: its phase always comes from IAudienceSessions, and Current
// is filled only when the phase is live. It returns null when the deck has no session at all.
public interface IAudienceRelay
{
    Task<AudienceState?> CurrentAsync(string deckId);

    // Go live opened a session (J2): start following the deck's presentation state.
    Task StartedAsync(string deckId);

    // The session ended (J2): send audienceEnded to the guests and stop following.
    Task EndedAsync(string deckId);

    // Something guests see changed outside the presentation state (a poll's phase, V3): compose the deck's current state
    // again and publish it, as a move would. Does nothing when the deck's session is not live.
    Task RefreshAsync(string deckId);
}
