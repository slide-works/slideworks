using System.Text.Json;

namespace SlideWorks.Core.Slides;

// What the layouts' rules share. Each layout's own rule (counts that must agree, and whether the text
// fits the slide: SlideFit; the pixel figures are the mockup CSS) is in its file under Layouts/.
internal static class LayoutRules
{
    // .callout: 28 px/1.35 bold text in 28/36 px padding, plus the 22–30 px gap above it.
    public static double CalloutHeight(JsonElement callout, double width, FitWidths widths) =>
        30 + 56 + (SlideFit.Lines(callout.GetProperty("text").GetString()!, width - 72, 28, widths.Head) * 37.8);
}
