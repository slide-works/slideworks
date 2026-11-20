using System.Text.Json;

namespace SlideWorks.WebApi.Features.Decks;

// A new deck. POST /api/decks sends the title and optionally a template and palette (K4, L6); create_deck may also
// send the subtitle, author and parts (K6, L7, K19: each a string or an object). A null template or palette takes LookRules' default.
// Like is a deck id whose look the new deck copies; it refuses a template, palette or colors in the same call.
// Every field is nullable so a missing title reaches DeckCommands and comes back as 400 { error }.
public sealed record CreateDeckRequest(
    string? Title,
    string? Subtitle = null,
    string? Author = null,
    IReadOnlyList<JsonElement>? Parts = null,
    string? Template = null,
    string? Palette = null,
    IReadOnlyDictionary<string, string>? Colors = null,
    string? Like = null);
