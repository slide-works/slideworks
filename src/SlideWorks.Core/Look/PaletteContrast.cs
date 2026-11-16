namespace SlideWorks.Core.Look;

// The contrast test every palette passes (L2, SW-2's "Contrast test"). The pair names are frozen.
public static class PaletteContrast
{
    private const double TEXT = 4.5;
    private const double LARGE_TEXT = 3.0;

    private static readonly (string Pair, string Fg, string Bg, double Bar)[] _pairs =
    [
        ("body", "ink", "paper", TEXT),
        ("secondary", "muted", "paper", TEXT),
        ("card-bold", "accent-dark", "card", TEXT),
        ("headline-accent", "accent", "paper", LARGE_TEXT),
        ("on-accent", "on-accent", "accent", TEXT),
        ("deep-dive-pill", "on-dd", "dd", TEXT),
        ("panel-accent", "panel-accent-ink", "panel-accent", TEXT),
        ("panel-positive", "panel-positive-ink", "panel-positive", TEXT),
        ("panel-negative", "panel-negative-ink", "panel-negative", TEXT),
        ("panel-inverse", "panel-inverse-ink", "panel-inverse", TEXT),
        ("badge-positive", "good-ink", "good-soft", TEXT),
        ("badge-negative", "bad-ink", "bad-soft", TEXT),
        ("dark-subtitle", "night-2", "night", TEXT),
    ];

    public static IReadOnlyList<ContrastCheck> Check(Palette palette) =>
        _pairs.Select(p =>
        {
            var fg = palette.Tokens[p.Fg];
            var bg = palette.Tokens[p.Bg];
            var ratio = Contrast.Ratio(fg, bg);
            return new ContrastCheck(p.Pair, fg, bg, ratio, p.Bar, ratio >= p.Bar);
        }).ToList();
}
