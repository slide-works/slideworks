namespace SlideWorks.WebApi.Realtime;

// The rate limit on guessing join codes (J3): misses from GET /api/join/{code} and from JoinAudience together count
// toward 10 a minute per caller key (CallerKey). Callers check Allow before the lookup (D20): once the misses are used up,
// every lookup is refused, a real code included, with 429 {"error":"too_many_attempts"} on the API and
// HubException("too_many_attempts") on the hub. Only a miss is counted; a hit never adds to it. Allow reserves a slot so
// parallel lookups can't all pass before any miss is counted: at most 10 lookups per caller are in flight or missed in a
// window. Every Allow that returned true is followed by Hit (refunds the slot) or Miss (keeps it); a lookup that fails
// with neither keeps its slot until the window ends.
public interface IJoinLookupLimiter
{
    // False once the caller's misses and lookups in flight have used up the window. True reserves a slot for this lookup.
    bool Allow(string callerKey);

    // A lookup found no session: the slot Allow reserved stays counted.
    void Miss(string callerKey);

    // A lookup found a session: the slot Allow reserved is given back.
    void Hit(string callerKey);
}
