namespace SlideWorks.LoadTest;

public static class Percentile
{
    // Nearest rank: the smallest value with at least p percent of the samples at or below it. Null with no samples.
    public static double? Of(IReadOnlyList<double> samples, double p)
    {
        if (samples.Count == 0)
        {
            return null;
        }
        var sorted = samples.Order().ToList();
        var rank = (int)Math.Ceiling(p / 100 * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }
}
