namespace SlideWorks.WebApi.Realtime;

// The rate limit on guessing join codes (J3): misses from GET /api/join/{code} and from JoinAudience together count
// toward 10 a minute per caller key (CallerKey). Callers check Allow before the lookup (D20): once the misses are used up,
// every lookup is refused, a real code included, with 429 {"error":"too_many_attempts"} on the API and
// HubException("too_many_attempts") on the hub. Only a miss is counted; a hit never adds to it.
// Allow registers the lookup as in flight, and Hit or Miss settles it: a caller can have a bounded number in flight at once
// (a room of real guests is not refused), and a lookup that fails with neither is forgotten after a window or two. A lookup that settles in a later window than it
// started in still releases its own slot.
public interface IJoinLookupLimiter
{
    // False once the caller's misses are used up or it has too many lookups in flight. True registers this lookup as in flight.
    bool Allow(string callerKey);

    // A lookup found no session: it is settled and counts as a miss.
    void Miss(string callerKey);

    // A lookup found a session: it is settled and counts for nothing.
    void Hit(string callerKey);
}
