namespace SlideWorks.WebApi.Features.Look.Views;

// A deck's resolved look (L6, T7), in the play view, the deck page, get_deck's render and the card view. Logo is the
// deck's own; Fonts, Logos, Art, Ornament, Extras and CustomLayouts are what its template brings, empty for a built-in.
// TopBar is "none" or "accent": the deck's override, else the template's. TemplateRef is the ref the deck stores, with
// the version it is pinned to and the template's latest (both null on a built-in). FooterDateLabel is the deck's last
// change as a server-formatted day. JoinBar is whether the join bar is on (J6). TemplateMissing is true when the deck's
// template could not be resolved and the default look stands in for it.
public sealed record LookView(
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
    bool TemplateMissing);
