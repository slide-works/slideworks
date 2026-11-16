namespace SlideWorks.WebApi.Features.Decks;

// A new deck. POST /api/decks sends only the title (K4); create_deck may also send the subtitle, author and parts (K6).
// Every field is nullable so a missing title reaches DeckCommands and comes back as 400 { error }.
public sealed record CreateDeckRequest(string? Title, string? Subtitle = null, string? Author = null, IReadOnlyList<string>? Parts = null);
