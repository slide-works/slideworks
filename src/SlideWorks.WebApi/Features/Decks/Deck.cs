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

    // The deck's look (L5): a template and a palette by name, from ITemplateCatalogue and IPaletteCatalogue.
    public required string Template { get; set; }

    public required string Palette { get; set; }

    // An uploaded image of this deck shown as the logo, or null for none.
    public string? LogoImageId { get; set; }

    // Starts from the template's chrome.draft; the chrome shows a draft mark while it is set.
    public bool Draft { get; set; }

    // Chrome overrides { tracker?, footer?, progress? } as JSON; null (or an absent key) means the template's default.
    public string? ChromeJson { get; set; }
}
