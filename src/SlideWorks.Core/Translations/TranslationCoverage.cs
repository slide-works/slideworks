namespace SlideWorks.Core.Translations;

// Counts the lines of merged slides by state (K3). Every coverage count on the wire comes from here.
public static class TranslationCoverage
{
    public static LanguageCoverage Of(IEnumerable<MergedSlide> slides)
    {
        var lines = 0;
        var current = 0;
        var stale = 0;
        var missing = 0;
        foreach (var line in slides.SelectMany(slide => slide.Lines))
        {
            lines++;
            switch (line.State)
            {
                case LineState.Current:
                    current++;
                    break;
                case LineState.Stale:
                    stale++;
                    break;
                case LineState.Missing:
                    missing++;
                    break;
            }
        }

        return new LanguageCoverage(lines, current, stale, missing, lines == 0 ? 100 : current * 100 / lines);
    }
}
