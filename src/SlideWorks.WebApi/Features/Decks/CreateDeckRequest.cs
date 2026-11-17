namespace SlideWorks.WebApi.Features.Decks;

// A new deck. POST /api/decks sends the title and optionally a template and palette (K4, L6); create_deck may also
// send the subtitle, author and parts (K6, L7). A null template or palette takes LookRules' default.
// Every field is nullable so a missing title reaches DeckCommands and comes back as 400 { error }.
public sealed record CreateDeckRequest(
    string? Title,
    string? Subtitle = null,
    string? Author = null,
    IReadOnlyList<string>? Parts = null,
    string? Template = null,
    string? Palette = null);
