using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// A palette's contrast summary (L2, L6): whether every pair meets its bar, and the pair with the least headroom
// (the smallest ratio divided by its bar), with the ratio rounded to one decimal (SW-60 D9).
public sealed record ContrastView(bool Pass, ContrastLowestView Lowest)
{
    public static ContrastView From(Palette palette)
    {
        var checks = PaletteContrast.Check(palette);
        var lowest = checks.MinBy(check => check.Ratio / check.Bar)!;
        return new ContrastView(
            checks.All(check => check.Pass),
            new ContrastLowestView(lowest.Pair, Math.Round(lowest.Ratio, 1, MidpointRounding.AwayFromZero), lowest.Bar));
    }
}
