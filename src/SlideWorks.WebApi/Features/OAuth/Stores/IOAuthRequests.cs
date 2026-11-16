namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Authorize requests between GET /oauth/authorize and the presenter's answer on the consent screen. A request belongs to
// the presenter who was signed in when it was made, lives OAuthLifetimes.Request and is used once.
public interface IOAuthRequests
{
    // Returns the request id the consent screen posts back.
    Task<string> CreateAsync(PendingAuthorization pending);

    // Reads a request without consuming it, for the consent screen. Null in exactly the cases ConsumeAsync returns null.
    Task<PendingAuthorization?> FindAsync(string requestId, Guid presenterId);

    // Atomic and single use. Null when the id is unknown, expired, already consumed, or belongs to another presenter;
    // another presenter's attempt leaves the request unconsumed.
    Task<PendingAuthorization?> ConsumeAsync(string requestId, Guid presenterId);
}
