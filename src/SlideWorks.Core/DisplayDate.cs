using System.Globalization;

namespace SlideWorks.Core;

// Pure, dependency-free logic lives in this project; only SlideWorks.WebApi and
// SlideWorks.UnitTest reference it. The SPA never builds Date objects, so every date
// it shows is formatted here (.claude/skills/no-browser-datetime).
public static class DisplayDate
{
    private static readonly TimeSpan MINUTE = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HOUR = TimeSpan.FromHours(1);
    private static readonly TimeSpan DAY = TimeSpan.FromDays(1);
    private const int DAYS_SHOWN_AS_AGO = 7;

    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    // A day for a slide footer, in UTC: "16 Nov 2026".
    public static string Day(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // How long ago value was, as of now, for lists and change logs: "just now", "2 min ago", "1 h ago", "Yesterday",
    // "3 days ago", then the UTC date ("2026-01-02"). It counts elapsed time, not calendar days, because the server
    // doesn't know the viewer's time zone. A value after now (clock skew) reads "just now".
    public static string Ago(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;
        if (elapsed < MINUTE)
        {
            return "just now";
        }
        if (elapsed < HOUR)
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }
        if (elapsed < DAY)
        {
            return $"{(int)elapsed.TotalHours} h ago";
        }
        if (elapsed < 2 * DAY)
        {
            return "Yesterday";
        }
        if (elapsed < DAYS_SHOWN_AS_AGO * DAY)
        {
            return $"{(int)elapsed.TotalDays} days ago";
        }
        return value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
