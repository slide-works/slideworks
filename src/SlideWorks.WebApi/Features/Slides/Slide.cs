namespace SlideWorks.WebApi.Features.Slides;

public class Slide
{
    public Guid Id { get; set; }

    public required string DeckId { get; set; }

    // 0-based among the deck's live slides.
    public int Position { get; set; }

    public required string Layout { get; set; }

    // 1-based part number, or null for a slide outside every part.
    public int? Part { get; set; }

    public required string SlotsJson { get; set; }

    public string? Notes { get; set; }

    // The slide's translated lines by language and slot path (SW-883 K2); null means none. Written only by Features/Translations
    // and the slide edits that carry the lines with the slide.
    public string? TranslationsJson { get; set; }

    public int Steps { get; set; }

    public long Revision { get; set; }

    // A deep dive (D4): the slide shows the deep-dive mark, and its notes read with a skip prefix (D16).
    public bool DeepDive { get; set; }

    // "light" or "dark" when the slide picks its surface, or null for the layout's own.
    public string? Surface { get; set; }

    // Set when the slide is deleted; deleted slides are kept, never removed.
    public DateTimeOffset? DeletedAt { get; set; }
}
