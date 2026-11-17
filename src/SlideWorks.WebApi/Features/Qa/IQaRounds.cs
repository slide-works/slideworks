namespace SlideWorks.WebApi.Features.Qa;

// The live session's Q&A round (Q2, Q6): one Q&A poll per deck, one open Q&A round per session. Per request, because it uses
// the database.
public interface IQaRounds
{
    // Go live: opens the session's Q&A round unless the deck has questions off. A session that already has one opens nothing.
    Task OpenAsync(string deckId, Guid sessionId);

    // End: closes the session's open Q&A round, if any. Called from LiveCommands.EndAsync before the session ends.
    Task CloseAsync(string deckId, Guid sessionId);

    // Questions left off during a live session: opens the live session's Q&A round if it has none. Does nothing when the deck
    // isn't live.
    Task EnsureAsync(string deckId);
}
