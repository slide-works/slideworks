namespace SlideWorks.WebApi.Features.Sharing;

// A named viewer of a deck shared with named people.
public class DeckViewer
{
    public required string DeckId { get; set; }

    public required string Email { get; set; }
}
