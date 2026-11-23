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

    public static string Format(DateTimeOffset value) => Format(value, TimeZoneInfo.Utc);

    // The same label in a person's zone, with the offset in force at value: "2026-01-02 09:00 UTC+07" or
    // "2026-01-02 09:30 UTC+05:30". UTC reads "UTC".
    public static string Format(DateTimeOffset value, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(value, zone);
        return $"{local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} {OffsetLabel(zone, local.Offset)}";
    }

    // A day for a slide footer, in UTC: "16 Nov 2026".
    public static string Day(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // How long ago value was, as of now, for lists and change logs: "just now", "2 min ago", "1 h ago", "Yesterday",
    // "3 days ago", then the UTC date ("2026-01-02"). It counts elapsed time, not calendar days, because the server
    // doesn't know the viewer's time zone. A value after now (clock skew) reads "just now".
    public static string Ago(DateTimeOffset value, DateTimeOffset now) => Ago(value, now, TimeZoneInfo.Utc);

    // As above, with the date shown in the person's zone once it is too old for "N days ago".
    public static string Ago(DateTimeOffset value, DateTimeOffset now, TimeZoneInfo zone)
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
        return TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string OffsetLabel(TimeZoneInfo zone, TimeSpan offset)
    {
        if (zone.Equals(TimeZoneInfo.Utc))
        {
            return "UTC";
        }
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var magnitude = offset.Duration();
        var hours = magnitude.Hours.ToString("00", CultureInfo.InvariantCulture);
        return magnitude.Minutes == 0
            ? $"UTC{sign}{hours}"
            : $"UTC{sign}{hours}:{magnitude.Minutes.ToString("00", CultureInfo.InvariantCulture)}";
    }
}
