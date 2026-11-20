namespace SlideWorks.Core.Look.Manifest;

// The palette a manifest brings. In the file it is one object: the 42 token keys, then an optional extras list.
// Every value is # plus 6 hex digits, because a custom layout's CSS may read it through a token.
public sealed record ManifestPalette
{
    // The keys a manifest's palette must hold, in file order: a built-in palette's tokens plus accent-2.
    public static IReadOnlyList<string> Keys { get; } =
    [
        "paper", "card", "chip", "grid", "ink", "muted",
        "accent", "accent-2", "accent-dark", "accent-light", "accent-glow", "accent-past", "on-accent",
        "good", "good-soft", "good-ink", "bad", "bad-soft", "bad-ink", "warn", "warn-soft", "warn-ink",
        "dd", "on-dd", "dd-on-night",
        "night", "night-ink", "night-2", "night-muted", "night-line", "night-glow",
        "number-from", "number-to", "stamp",
        "panel-accent", "panel-accent-ink", "panel-positive", "panel-positive-ink",
        "panel-negative", "panel-negative-ink", "panel-inverse", "panel-inverse-ink",
    ];

    public required IReadOnlyDictionary<string, string> Tokens { get; init; }
    public required IReadOnlyList<ManifestExtra> Extras { get; init; }
}
