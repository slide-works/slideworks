using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Render.Print;

// TODO: the real word-cloud and open-text results reader replaces this stub and this file is deleted.
[RegisterPerRequest(For = typeof(IPrintOpenResponseResults))]
public class StubPrintOpenResponseResults : IPrintOpenResponseResults
{
    public Task<PrintOpenResponses> ReadAsync(string deckId, Guid sessionId, CancellationToken ct) =>
        Task.FromResult(new PrintOpenResponses([], []));
}
