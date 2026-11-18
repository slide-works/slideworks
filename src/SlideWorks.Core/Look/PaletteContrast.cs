namespace SlideWorks.Core.Look;

// The contrast test every palette passes (L2, SW-2's "Contrast test"). The pair names are frozen.
// soft-accent, soft-inverse and deep-dive-list were added after (L2 amended in SW-72's fix round): the soft
// accent and inverse panels' text, and the divider's deep-dive pills on the dark slide. on-accent-2 is the
// second accent as a fill: it sits after on-accent, so a tie for the lowest pair still names on-accent.
public static class PaletteContrast
{
    private const double TEXT = 4.5;
    private const double LARGE_TEXT = 3.0;

    private static readonly (string Pair, string Fg, string Bg, double Bar, string? Over, double? Share)[] _pairs =
    [
        ("body", "ink", "paper", TEXT, null, null),
        ("secondary", "muted", "paper", TEXT, null, null),
        ("card-bold", "accent-dark", "card", TEXT, null, null),
        ("headline-accent", "accent", "paper", LARGE_TEXT, null, null),
        ("on-accent", "on-accent", "accent", TEXT, null, null),
        ("on-accent-2", "on-accent", "accent-2", TEXT, null, null),
        ("deep-dive-pill", "on-dd", "dd", TEXT, null, null),
        ("panel-accent", "panel-accent-ink", "panel-accent", TEXT, null, null),
        ("panel-positive", "panel-positive-ink", "panel-positive", TEXT, null, null),
        ("panel-negative", "panel-negative-ink", "panel-negative", TEXT, null, null),
        ("panel-inverse", "panel-inverse-ink", "panel-inverse", TEXT, null, null),
        ("badge-positive", "good-ink", "good-soft", TEXT, null, null),
        ("badge-negative", "bad-ink", "bad-soft", TEXT, null, null),
        ("dark-subtitle", "night-2", "night", TEXT, null, null),
        ("soft-accent", "accent-dark", "accent-light", TEXT, null, null),
        ("soft-inverse", "night-ink", "night", TEXT, null, null),
        ("deep-dive-list", "dd-on-night", "night", TEXT, null, null),
        ("dark-kicker", "accent-glow", "night", TEXT, null, null),
        ("dark-chrome", "night-muted", "night", TEXT, null, null),
        ("dark-soft-accent", "night-ink", "accent", TEXT, "night", 0.24),
        ("dark-soft-positive", "night-ink", "good", TEXT, "night", 0.22),
        ("dark-soft-negative", "night-ink", "bad", TEXT, "night", 0.22),
        // The pin on a solid panel: the panel's ink on night at 25% over the panel (Pin.module.css).
        ("pin-solid-accent", "panel-accent-ink", "night", TEXT, "panel-accent", 0.25),
        ("pin-solid-positive", "panel-positive-ink", "night", TEXT, "panel-positive", 0.25),
        ("pin-solid-negative", "panel-negative-ink", "night", TEXT, "panel-negative", 0.25),
    ];

    // The table for src/contracts/contrast-pairs.json, in order. Over and Share are set on a mixed-ground pair only:
    // the ground is then Back mixed at Share over Over. The dark soft pairs and the solid pin pairs are mixed.
    public static IReadOnlyList<(string Name, string Fore, string Back, double Needed, string? Over, double? Share)> Pairs { get; } =
        _pairs.ToList();

    public static IReadOnlyList<ContrastCheck> Check(Palette palette) =>
        _pairs.Select(p =>
        {
            var fg = palette.Tokens[p.Fg];
            var bg = palette.Tokens[p.Bg];
            if (p.Over is not null && p.Share is { } share)
            {
                bg = Contrast.Mix(bg, palette.Tokens[p.Over], share);
            }

            var ratio = Contrast.Ratio(fg, bg);
            return new ContrastCheck(p.Pair, fg, bg, ratio, p.Bar, ratio >= p.Bar);
        }).ToList();
}
