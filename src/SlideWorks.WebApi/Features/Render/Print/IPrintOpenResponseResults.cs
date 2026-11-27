namespace SlideWorks.WebApi.Features.Render.Print;

// The word-cloud and open-text results of one session for the print view.
public interface IPrintOpenResponseResults
{
    Task<PrintOpenResponses> ReadAsync(string deckId, Guid sessionId, CancellationToken ct);
}
