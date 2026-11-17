namespace SlideWorks.WebApi.Tests.RateLimits;

// The anonymous endpoints with a per-caller budget: Token is POST /oauth/token and /oauth/revoke (one budget, 60 a minute),
// Registration is POST /oauth/register (10 a minute).
public enum LimitedEndpoint
{
    Token,
    Registration,
}
