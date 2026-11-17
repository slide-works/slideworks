namespace SlideWorks.WebApi.Realtime;

// The rate limit on guessing join codes (J3): misses from GET /api/join/{code} and from JoinAudience together count
// toward 10 a minute per caller key (CallerKey). Callers check Allow before the lookup (D20): once the misses are used up,
// every lookup is refused, a real code included, with 429 {"error":"too_many_attempts"} on the API and
// HubException("too_many_attempts") on the hub. Only a miss is counted; a hit never adds to it.
public interface IJoinLookupLimiter
{
    // False once the caller has used up its misses for the window.
    bool Allow(string callerKey);

    // A lookup found no session.
    void Miss(string callerKey);
}
