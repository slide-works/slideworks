using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// The deck page's look (L6): a LookView plus the deck's own chrome overrides, for the Look panel's switches, and the
// palette's contrast summary.
public sealed record DeckLookView(
    LookTemplateView Template,
    LookPaletteView Palette,
    LookLogoView? Logo,
    bool Draft,
    string FooterDateLabel,
    bool JoinBar,
    ChromeOverridesView ChromeOverrides,
    ContrastView Contrast)
{
    public static DeckLookView From(LookView look, ChromeOverridesView overrides, Palette palette)
        => new(look.Template, look.Palette, look.Logo, look.Draft, look.FooterDateLabel, look.JoinBar, overrides, ContrastView.From(palette));
}
