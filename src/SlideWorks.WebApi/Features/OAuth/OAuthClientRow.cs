namespace SlideWorks.WebApi.Features.OAuth;

// A client registered through dynamic client registration.
public class OAuthClientRow
{
    public required string ClientId { get; set; }

    public required string ClientName { get; set; }

    public required string RedirectUrisJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
