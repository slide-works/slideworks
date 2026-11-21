namespace SlideWorks.Core.Look;

// The card shadow of a deck's look (SW-148, the SW-2 mockups' dark lift): the template's own shadow, except on a
// palette whose paper is dark (relative luminance under 0.2, as the mockups decide it). A template's shadow is drawn in
// its dark ink, which doesn't show on dark paper, so there a card is lifted by a faint light ring and a deeper black
// shadow instead. A template whose shadow is none stays flat on any paper. Dark passes ManifestShadow, as every shadow
// a custom layout's CSS can reach through --sw-lift does.
public static class CardLift
{
    public const string DARK = "0 0 0 1px rgb(255 255 255 / 0.05), 0 14px 36px rgb(0 0 0 / 0.35)";

    private const double DARK_PAPER_LUMINANCE = 0.2;
    private const string NONE = "none";

    public static string For(string templateShadow, string? paper)
        => templateShadow != NONE && paper is not null && Contrast.TryLuminance(paper, out var luminance) && luminance < DARK_PAPER_LUMINANCE
            ? DARK
            : templateShadow;
}
