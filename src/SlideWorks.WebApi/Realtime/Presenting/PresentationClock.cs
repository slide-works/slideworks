namespace SlideWorks.WebApi.Realtime.Presenting;

// JoinPresentation's clockMinutes (P2, D7): minutes since local midnight (0-1439) at a moment, in the caller's IANA time
// zone. An unknown or empty time zone falls back to UTC. The browser advances it with performance.now() deltas.
public static class PresentationClock
{
    public static int MinutesSinceMidnight(DateTimeOffset now, string? timeZone)
    {
        var zone = !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found)
            ? found
            : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(now, zone);
        return (local.Hour * 60) + local.Minute;
    }
}
