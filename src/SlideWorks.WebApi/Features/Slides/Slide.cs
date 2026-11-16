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

    public int Steps { get; set; }

    public long Revision { get; set; }

    // Set when the slide is deleted; deleted slides are kept, never removed.
    public DateTimeOffset? DeletedAt { get; set; }
}
