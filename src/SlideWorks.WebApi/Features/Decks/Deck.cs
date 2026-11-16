namespace SlideWorks.WebApi.Features.Decks;

public class Deck
{
    // 12-character random base62; the only id in every URL, API, tool and hub call.
    public required string Id { get; set; }

    // Cosmetic, from the title.
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public string? Subtitle { get; set; }

    public string? Author { get; set; }

    // The deck's parts as a JSON array of strings.
    public required string PartsJson { get; set; }

    public Guid OwnerId { get; set; }

    public DeckSharing Sharing { get; set; }

    // The deck's change counter; each change stamps the slides it touched with the new value.
    public long Revision { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
