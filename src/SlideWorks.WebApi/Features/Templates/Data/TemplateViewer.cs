namespace SlideWorks.WebApi.Features.Templates.Data;

// A named viewer of a template shared with named people, as DeckViewer is for a deck.
public class TemplateViewer
{
    public required string TemplateId { get; set; }

    public required string Email { get; set; }
}
