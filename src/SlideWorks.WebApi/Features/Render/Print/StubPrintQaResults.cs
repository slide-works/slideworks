using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Render.Print;

// TODO: the real Q&A results reader replaces this stub and this file is deleted.
[RegisterPerRequest(For = typeof(IPrintQaResults))]
public class StubPrintQaResults : IPrintQaResults
{
    public Task<PrintQa?> ReadAsync(string deckId, Guid sessionId, CancellationToken ct) => Task.FromResult<PrintQa?>(null);
}
