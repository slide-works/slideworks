namespace SlideWorks.WebApi.Features.OpenResponses.Data;

// One round of an open-response slide in one live session. Number is 1-based per slide and session, and New round adds the
// next one. Revision goes up on every change a guest or the presenter makes, so a submit from an older view is stale.
public class OpenRound
{
    public Guid Id { get; set; }
    public Guid OpenResponseId { get; set; }
    public Guid SessionId { get; set; }
    public int Number { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? RevealedAt { get; set; }
}
