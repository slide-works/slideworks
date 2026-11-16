namespace SlideWorks.WebApi.Features.OAuth.Stores;

// An authorize request that has passed its checks and waits for the presenter's consent. State is the client's own value,
// handed back on the redirect unchanged.
public sealed record PendingAuthorization(
    string ClientId,
    Guid PresenterId,
    string RedirectUri,
    string? State,
    string CodeChallenge,
    string Scope,
    string Resource);
