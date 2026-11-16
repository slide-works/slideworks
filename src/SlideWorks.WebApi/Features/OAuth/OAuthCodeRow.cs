namespace SlideWorks.WebApi.Features.OAuth;

// An authorization code, stored by its SHA-256 hash. Single use.
public class OAuthCodeRow
{
    public required string CodeHash { get; set; }

    public required string ClientId { get; set; }

    public Guid PresenterId { get; set; }

    public required string RedirectUri { get; set; }

    public required string CodeChallenge { get; set; }

    public required string Scope { get; set; }

    public required string Resource { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RedeemedAt { get; set; }
}
