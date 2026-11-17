namespace SlideWorks.WebApi.Realtime;

// Finds an audience session by its join code or its deck (J3). This is the only place the phase rule lives:
//   live   while a session is open;
//   ended  when the last session ended under 2 hours ago;
//   lobby  otherwise, but only for a deck that has a join slide, has the join bar on, or has gone live before (D17).
// Any other code or deck gives null, which means "no session". Nothing else computes a phase.
//
// The real implementation reads the database, so it is registered per request. A singleton (the relay, the hub's bus) must
// not hold it: open a scope per call with IServiceScopeFactory, as CachingCardRenderer does.
public interface IAudienceSessions
{
    Task<AudienceSessionRef?> FindAsync(string code);

    Task<AudienceSessionRef?> ForDeckAsync(string deckId);
}
