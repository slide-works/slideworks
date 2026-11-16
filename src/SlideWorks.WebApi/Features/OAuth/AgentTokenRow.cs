namespace SlideWorks.WebApi.Features.OAuth;

// An access or refresh token, stored by its SHA-256 hash. Tokens issued from one code share a family, and revoking
// one revokes the family.
public class AgentTokenRow
{
    public Guid Id { get; set; }

    public required string TokenHash { get; set; }

    public AgentTokenKind Kind { get; set; }

    public Guid FamilyId { get; set; }

    public required string ClientId { get; set; }

    public Guid PresenterId { get; set; }

    public required string Scope { get; set; }

    public required string Resource { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    // Refresh tokens only: set when it is rotated, so a second use is a replay.
    public DateTimeOffset? UsedAt { get; set; }
}
