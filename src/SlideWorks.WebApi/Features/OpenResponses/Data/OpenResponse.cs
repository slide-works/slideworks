using SlideWorks.Core.OpenResponses;

namespace SlideWorks.WebApi.Features.OpenResponses.Data;

// A word-cloud or open-text slide's open-response record (SW-8 D1), one per slide. SlideId's foreign key is Restrict, and a
// slide is soft-deleted (DeletedAt), so deleting the slide or changing its layout keeps the rows and is not blocked (D12).
public class OpenResponse
{
    public Guid Id { get; set; }
    public required string DeckId { get; set; }
    public Guid SlideId { get; set; }
    public OpenResponseKind Kind { get; set; }
}
