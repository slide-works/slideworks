using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// set_parts's result: the deck's parts after the change.
public sealed record SetPartsResult([property: JsonPropertyName("parts")] IReadOnlyList<DeckPart> Parts);
