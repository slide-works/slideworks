using SlideWorks.WebApi.Features.Decks.Mcp;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// set_look's result (L7): the look in get_deck's terms (SW-63 D3), so an agent can pass it back, and a warning for each
// slide whose layout is outside the deck's template.
public sealed record SetLookResult(GetDeckLook Look, IReadOnlyList<string> Warnings);
