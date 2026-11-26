using SlideWorks.WebApi.Features.Render.Print;

namespace SlideWorks.WebApi.Tests.Render;

// IPrintQaResults in front of the real one that counts ReadAsync calls, so a test can wait until a number of print views
// have been read (the cache reads the view once per download, before it joins or starts a render).
public sealed class CountingPrintQaResults(IPrintQaResults inner, QaReadCounter counter) : IPrintQaResults
{
    public Task<PrintQa?> ReadAsync(string deckId, Guid sessionId, CancellationToken ct)
    {
        counter.Record();
        return inner.ReadAsync(deckId, sessionId, ct);
    }
}
