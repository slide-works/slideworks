using SlideWorks.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look;

// Builds a deck's LookView (L6, T7) from its resolved template and its stored palette, logo, draft and chrome overrides.
// The template is the one ITemplates answered for the deck. When that is not the template the deck names (the ref did
// not resolve, so the default stands in), the look says so in TemplateMissing and still names the deck's stored ref.
// A stored palette nobody knows is a server fault, not a fallback, unless the template itself is missing.
public static class LookViews
{
    public static LookView From(Deck deck, ResolvedTemplate template, IPaletteCatalogue palettes)
    {
        var overrides = ChromeOverridesView.From(deck.ChromeJson);
        var missing = IsMissing(deck, template);
        return Build(
            template,
            overrides,
            PaletteTokens.Apply(PaletteOf(deck, template, palettes), deck.PaletteTokensJson),
            LookLogoView.From(deck.LogoImageId),
            missing ? deck.Template : template.Ref,
            missing ? null : deck.TemplateVersion,
            deck.Draft,
            DisplayDate.Day(deck.UpdatedAt),
            missing);
    }

    // The deck page's look: the LookView plus the chrome overrides and the palette's contrast.
    public static DeckLookView ForPage(Deck deck, ResolvedTemplate template, IPaletteCatalogue palettes)
        => DeckLookView.From(From(deck, template, palettes), ChromeOverridesView.From(deck.ChromeJson), PaletteTokens.Apply(PaletteOf(deck, template, palettes), deck.PaletteTokensJson));

    // The look of no deck in particular, for the generic share card: the default template and its palette.
    public static LookView Default(ResolvedTemplate template, IPaletteCatalogue palettes, DateTimeOffset now)
        => Build(template, ChromeOverridesView.None, FallbackPalette(template, palettes), null, template.Ref, template.Version, template.Template.Chrome.Draft, DisplayDate.Day(now), false);

    // True when the template resolved for the deck is not the one the deck names: its ref did not resolve.
    public static bool IsMissing(Deck deck, ResolvedTemplate template) => !string.Equals(template.Ref, deck.Template, StringComparison.Ordinal);

    private static LookView Build(
        ResolvedTemplate template,
        ChromeOverridesView overrides,
        Palette palette,
        LookLogoView? logo,
        string templateRef,
        int? version,
        bool draft,
        string footerDateLabel,
        bool missing)
    {
        var look = template.Look;
        return new LookView(
            LookTemplateView.From(template.Template, overrides, palette.Tokens.GetValueOrDefault("paper")),
            LookPaletteView.From(palette),
            logo,
            look.Fonts.Select(font => LookFontView.From(template.Ref, font)).ToList(),
            LookLogosView.From(template.Ref, look),
            LookArtView.From(template.Ref, look),
            new LookOrnamentView(look.OrnamentColours),
            overrides.TopBar ?? look.TopBar,
            templateRef,
            version,
            template.LatestVersion,
            look.Extras.Select(LookExtraView.From).ToList(),
            [],
            draft,
            footerDateLabel,
            overrides.JoinBar,
            missing);
    }

    // The deck's palette: the template's own under the reserved name, else a built-in by name.
    private static Palette PaletteOf(Deck deck, ResolvedTemplate template, IPaletteCatalogue palettes)
    {
        var palette = string.Equals(deck.Palette, ResolvedCatalogues.OWN_PALETTE, StringComparison.Ordinal)
            ? OwnPalette(template)
            : palettes.Find(deck.Palette);
        if (palette is not null)
        {
            return palette;
        }
        return IsMissing(deck, template)
            ? FallbackPalette(template, palettes)
            : throw new InvalidOperationException($"Deck {deck.Id} has an unknown palette '{deck.Palette}'.");
    }

    // The palette a template gets when nothing picks one: its own, else the built-in it names, else the default.
    private static Palette FallbackPalette(ResolvedTemplate template, IPaletteCatalogue palettes)
        => OwnPalette(template) ?? (template.Template.Palette is { } name ? palettes.Find(name) : null) ?? palettes.Default;

    private static Palette? OwnPalette(ResolvedTemplate template)
        => template.OwnPalette is { } own ? own with { Name = ResolvedCatalogues.OWN_PALETTE } : null;
}
