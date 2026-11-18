using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// The deck page's look (L6): a LookView plus the deck's own chrome overrides, for the Look panel's switches, and the
// palette's contrast summary.
public sealed record DeckLookView(
    LookTemplateView Template,
    LookPaletteView Palette,
    LookLogoView? Logo,
    IReadOnlyList<LookFontView> Fonts,
    LookLogosView Logos,
    LookArtView Art,
    LookOrnamentView Ornament,
    string TopBar,
    string TemplateRef,
    int? TemplateVersion,
    int? LatestVersion,
    IReadOnlyList<LookExtraView> Extras,
    IReadOnlyList<LookCustomLayoutView> CustomLayouts,
    bool Draft,
    string FooterDateLabel,
    bool JoinBar,
    bool TemplateMissing,
    ChromeOverridesView ChromeOverrides,
    ContrastView Contrast)
{
    public static DeckLookView From(LookView look, ChromeOverridesView overrides, Palette palette)
        => new(
            look.Template,
            look.Palette,
            look.Logo,
            look.Fonts,
            look.Logos,
            look.Art,
            look.Ornament,
            look.TopBar,
            look.TemplateRef,
            look.TemplateVersion,
            look.LatestVersion,
            look.Extras,
            look.CustomLayouts,
            look.Draft,
            look.FooterDateLabel,
            look.JoinBar,
            look.TemplateMissing,
            overrides,
            ContrastView.From(palette));
}
