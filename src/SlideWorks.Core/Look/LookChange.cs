namespace SlideWorks.Core.Look;

// A change to a deck's look (L6 look-change.json, L8): each null field is left as it is.
public sealed record LookChange(
    string? Template = null,
    string? Palette = null,
    LogoChange? Logo = null,
    bool? Draft = null,
    ChromeChange? Chrome = null);
