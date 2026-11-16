using SlideWorks.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Look;

// Builds a deck's LookView (L6) from its stored template, palette, logo, draft and chrome overrides, through the
// catalogues. A stored name the catalogue doesn't know is a server fault, not a fallback to the default.
public static class LookViews
{
    public static LookView From(Deck deck, ITemplateCatalogue templates, IPaletteCatalogue palettes)
        => new(
            LookTemplateView.From(TemplateOf(deck, templates), ChromeOverridesView.From(deck.ChromeJson)),
            LookPaletteView.From(PaletteOf(deck, palettes)),
            LookLogoView.From(deck.LogoImageId),
            deck.Draft,
            DisplayDate.Day(deck.UpdatedAt));

    // The deck page's look: the LookView plus the chrome overrides and the palette's contrast.
    public static DeckLookView ForPage(Deck deck, ITemplateCatalogue templates, IPaletteCatalogue palettes)
        => DeckLookView.From(From(deck, templates, palettes), ChromeOverridesView.From(deck.ChromeJson), PaletteOf(deck, palettes));

    // The look of no deck in particular, for the generic share card: the default template and its palette.
    public static LookView Default(ITemplateCatalogue templates, IPaletteCatalogue palettes, DateTimeOffset now)
    {
        var look = new LookRules(templates, palettes).Resolve(null, null);
        return new LookView(
            LookTemplateView.From(look.Template!, ChromeOverridesView.None),
            LookPaletteView.From(look.Palette!),
            null,
            look.Template!.Chrome.Draft,
            DisplayDate.Day(now));
    }

    private static Template TemplateOf(Deck deck, ITemplateCatalogue templates)
        => templates.Find(deck.Template) ?? throw new InvalidOperationException($"Deck {deck.Id} has an unknown template '{deck.Template}'.");

    private static Palette PaletteOf(Deck deck, IPaletteCatalogue palettes)
        => palettes.Find(deck.Palette) ?? throw new InvalidOperationException($"Deck {deck.Id} has an unknown palette '{deck.Palette}'.");
}
