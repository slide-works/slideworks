namespace SlideWorks.WebApi.Realtime;

// The rate limit on guessing join codes (J3): misses from GET /api/join/{code} and from JoinAudience together count
// toward 10 a minute per caller key (CallerKey), then 429 {"error":"too_many_attempts"} on the API and
// HubException("too_many_attempts") on the hub. A hit is never limited and never counted.
public interface IJoinLookupLimiter
{
    // False once the caller has used up its misses for the window.
    bool Allow(string callerKey);

    // A lookup found no session.
    void Miss(string callerKey);
}
