namespace SlideWorks.Core.Look;

// One pair of a palette's contrast check (L2): the colours compared, their WCAG ratio and the bar.
public sealed record ContrastCheck(string Pair, string Fg, string Bg, double Ratio, double Bar, bool Pass);
