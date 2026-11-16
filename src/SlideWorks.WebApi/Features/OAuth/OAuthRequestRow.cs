namespace SlideWorks.WebApi.Features.OAuth;

// An authorization request waiting for the presenter's consent. Single use.
public class OAuthRequestRow
{
    public required string Id { get; set; }

    public required string ClientId { get; set; }

    public Guid PresenterId { get; set; }

    public required string RedirectUri { get; set; }

    public string? State { get; set; }

    public required string CodeChallenge { get; set; }

    public required string Scope { get; set; }

    public required string Resource { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
