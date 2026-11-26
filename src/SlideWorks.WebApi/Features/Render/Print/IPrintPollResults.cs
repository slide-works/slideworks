namespace SlideWorks.WebApi.Features.Render.Print;

// The poll results of one session for the print view.
public interface IPrintPollResults
{
    Task<IReadOnlyList<PrintPoll>> ReadAsync(string deckId, Guid sessionId, CancellationToken ct);
}
