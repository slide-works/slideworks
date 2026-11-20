using SlideWorks.Core.OpenResponses;

namespace SlideWorks.WebApi.Features.OpenResponses.Data;

// A word-cloud or open-text slide's open-response record (SW-8 D1), one per slide. SlideId has no foreign key: deleting the
// slide or changing its layout keeps the rows and must not be blocked by them (D12).
public class OpenResponse
{
    public Guid Id { get; set; }
    public required string DeckId { get; set; }
    public Guid SlideId { get; set; }
    public OpenResponseKind Kind { get; set; }
}
