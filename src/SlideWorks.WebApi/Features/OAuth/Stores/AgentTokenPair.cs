namespace SlideWorks.WebApi.Features.OAuth.Stores;

// The tokens POST /oauth/token hands out. ExpiresInSeconds is the access token's lifetime.
public sealed record AgentTokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds, string Scope);
