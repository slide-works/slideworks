using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Render.Print;

// TODO: the real poll results reader replaces this stub and this file is deleted.
[RegisterPerRequest(For = typeof(IPrintPollResults))]
public class StubPrintPollResults : IPrintPollResults
{
    public Task<IReadOnlyList<PrintPoll>> ReadAsync(string deckId, Guid sessionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PrintPoll>>([]);
}
