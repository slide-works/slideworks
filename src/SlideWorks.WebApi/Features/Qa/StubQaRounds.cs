using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real rounds: the session's Q&A poll and round, opened on Go live and closed on End.
// Does nothing: no Q&A round is opened or closed.
[RegisterPerRequest(For = typeof(IQaRounds))]
public sealed class StubQaRounds : IQaRounds
{
    public Task OpenAsync(string deckId, Guid sessionId) => Task.CompletedTask;

    public Task CloseAsync(string deckId, Guid sessionId) => Task.CompletedTask;

    public Task EnsureAsync(string deckId) => Task.CompletedTask;
}
