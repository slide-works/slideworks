using System.Globalization;

namespace SlideWorks.Core;

// Pure, dependency-free logic lives in this project; only SlideWorks.WebApi and
// SlideWorks.UnitTest reference it. The SPA never builds Date objects, so every date
// it shows is formatted here (.claude/skills/no-browser-datetime).
public static class DisplayDate
{
    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
