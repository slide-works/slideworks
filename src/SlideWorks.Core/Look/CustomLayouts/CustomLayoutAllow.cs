namespace SlideWorks.Core.Look.CustomLayouts;

// What a custom layout's markup and CSS may hold (T10): the security boundary's source. Every list says
// what may pass and none says what to block, so a name that is not here is refused. Names are compared
// exactly as written: an upper-case URL or POSITION is simply not on a list. The same lists are in
// src/contracts/custom-layout-allow.json, kept equal by a test.
public sealed record CustomLayoutAllow
{
    public required IReadOnlyList<string> Tags { get; init; }
    public required IReadOnlyList<string> Attributes { get; init; }
    public required IReadOnlyList<string> SlotTypes { get; init; }
    public required IReadOnlyList<string> BlockSlotTypes { get; init; }

    // Not visibility: it would un-hide a waiting step.
    public required IReadOnlyList<string> CssProperties { get; init; }
    public required IReadOnlyList<string> CssFunctions { get; init; }
    public required IReadOnlyList<string> CssUnits { get; init; }

    // Every bare word a value may hold.
    public required IReadOnlyList<string> CssKeywords { get; init; }

    // Only tokens whose value can only ever be a checked colour or a number with a unit. --sw-lift is
    // out because a template sets it from free text, which a layout could turn into a fetched URL.
    public required IReadOnlyList<string> Tokens { get; init; }
    public required IReadOnlyList<string> FontTokens { get; init; }

    public static CustomLayoutAllow Default { get; } = new()
    {
        Tags =
        [
            "div", "span", "p", "h2", "h3", "ul", "ol", "li", "b", "i", "em", "strong",
        ],
        Attributes =
        [
            "class", "data-slot", "data-each", "data-if", "data-var", "data-variant", "data-step",
        ],
        SlotTypes =
        [
            "text", "number", "enum", "list", "image",
        ],
        BlockSlotTypes =
        [
            "callout", "chip", "badge", "levels", "facts",
        ],
        CssProperties =
        [
            "display", "position", "top", "right", "bottom", "left", "inset", "z-index", "overflow", "box-sizing",
            "width", "height", "min-width", "max-width", "min-height", "max-height", "margin", "margin-top",
            "margin-right", "margin-bottom", "margin-left", "padding", "padding-top", "padding-right",
            "padding-bottom", "padding-left", "flex", "flex-direction", "flex-wrap", "flex-grow", "flex-shrink",
            "flex-basis", "justify-content", "align-items", "align-self", "gap", "row-gap", "column-gap",
            "grid-template-columns", "grid-template-rows", "grid-column", "grid-row", "font-family", "font-size",
            "font-weight", "font-style", "line-height", "letter-spacing", "text-align", "text-transform",
            "text-decoration", "white-space", "overflow-wrap", "color", "background", "background-color", "border",
            "border-top", "border-right", "border-bottom", "border-left", "border-radius", "box-shadow", "opacity",
            "mask", "transform", "transform-origin",
        ],
        CssFunctions =
        [
            "var", "calc", "min", "max", "clamp", "color-mix", "linear-gradient", "radial-gradient",
            "conic-gradient", "repeating-linear-gradient", "rotate", "translate", "repeat", "minmax",
        ],
        CssUnits =
        [
            "px", "em", "%", "fr", "deg",
        ],
        CssKeywords =
        [
            "auto", "none", "block", "inline-block", "flex", "grid", "static", "relative", "absolute", "hidden",
            "visible", "border-box", "row", "column", "wrap", "nowrap", "start", "end", "center", "flex-start",
            "flex-end", "space-between", "stretch", "normal", "bold", "italic", "uppercase", "underline",
            "line-through", "pre", "break-word", "solid", "dashed", "transparent", "currentColor", "span", "from",
            "at", "to", "in", "srgb", "circle", "top", "right", "bottom", "left",
        ],
        Tokens =
        [
            "--sw-paper", "--sw-card", "--sw-chip", "--sw-grid", "--sw-ink", "--sw-muted", "--sw-accent",
            "--sw-accent-2", "--sw-accent-dark", "--sw-accent-light", "--sw-accent-glow", "--sw-accent-past",
            "--sw-on-accent", "--sw-good", "--sw-good-soft", "--sw-good-ink", "--sw-bad", "--sw-bad-soft",
            "--sw-bad-ink", "--sw-warn", "--sw-warn-soft", "--sw-warn-ink", "--sw-dd", "--sw-on-dd",
            "--sw-dd-on-night", "--sw-night", "--sw-night-ink", "--sw-night-2", "--sw-night-muted",
            "--sw-night-line", "--sw-night-glow", "--sw-number-from", "--sw-number-to", "--sw-stamp",
            "--sw-panel-accent", "--sw-panel-accent-ink", "--sw-panel-positive", "--sw-panel-positive-ink",
            "--sw-panel-negative", "--sw-panel-negative-ink", "--sw-panel-inverse", "--sw-panel-inverse-ink",
            "--sw-radius-sm", "--sw-radius-md", "--sw-radius-lg", "--sw-radius-xl", "--sw-radius-pill",
            "--sw-space-1", "--sw-space-2", "--sw-space-3", "--sw-space-4", "--sw-space-5", "--sw-space-6",
            "--sw-slide-width", "--sw-slide-height", "--sw-slide-pad-top", "--sw-slide-pad-x",
            "--sw-slide-pad-bottom", "--sw-extra-1", "--sw-extra-2", "--sw-extra-3", "--sw-extra-4",
            "--sw-extra-1-ink", "--sw-extra-2-ink", "--sw-extra-3-ink", "--sw-extra-4-ink",
        ],
        FontTokens =
        [
            "--sw-font-head", "--sw-font-body", "--sw-font-mono",
        ],
    };
}
