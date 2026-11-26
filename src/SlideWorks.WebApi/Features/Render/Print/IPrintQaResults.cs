namespace SlideWorks.WebApi.Features.Render.Print;

// The Q&A results of one session for the print view, or null when the session had no Q&A round.
public interface IPrintQaResults
{
    Task<PrintQa?> ReadAsync(string deckId, Guid sessionId, CancellationToken ct);
}
