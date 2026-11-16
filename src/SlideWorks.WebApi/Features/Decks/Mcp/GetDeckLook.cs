using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck's look (L7), in set_look's terms so an agent can pass it back: the template and palette names, the logo's
// image id (or null), the draft flag and the deck's own chrome overrides (a null field is the template's default).
public sealed record GetDeckLook(string Template, string Palette, string? Logo, bool Draft, ChromeOverridesView Chrome)
{
    public static GetDeckLook From(Deck deck)
        => new(deck.Template, deck.Palette, deck.LogoImageId, deck.Draft, ChromeOverridesView.From(deck.ChromeJson));
}
