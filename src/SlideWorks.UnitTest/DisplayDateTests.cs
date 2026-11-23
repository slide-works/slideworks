using SlideWorks.Core;

namespace SlideWorks.UnitTest;

[TestFixture]
public class DisplayDateTests
{
    [Test]
    public void Format_WhenUtc_ReturnsMinutePrecisionUtcLabel()
    {
        var value = new DateTimeOffset(2026, 1, 2, 3, 4, 59, TimeSpan.Zero);

        DisplayDate.Format(value).ShouldBe("2026-01-02 03:04 UTC");
    }

    [Test]
    public void Format_WhenOffsetGiven_ConvertsToUtcFirst()
    {
        var value = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.FromHours(10));

        DisplayDate.Format(value).ShouldBe("2026-01-01 17:04 UTC");
    }

    [Test]
    public void Format_WhenZoneGiven_ShouldShowLocalTime()
    {
        var value = new DateTimeOffset(2026, 1, 2, 20, 30, 0, TimeSpan.Zero);
        var bangkok = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        DisplayDate.Format(value, bangkok).ShouldBe("2026-01-03 03:30 UTC+07");
        DisplayDate.Format(value, kolkata).ShouldBe("2026-01-03 02:00 UTC+05:30");
        DisplayDate.Format(value, newYork).ShouldBe("2026-01-02 15:30 UTC-05");
        DisplayDate.Format(value, TimeZoneInfo.Utc).ShouldBe("2026-01-02 20:30 UTC");
    }

    [Test]
    public void Format_WhenZoneObservesSummerTime_ShouldShowOffsetInForceThen()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        DisplayDate.Format(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), london).ShouldBe("2026-07-01 13:00 UTC+01");
        DisplayDate.Format(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), london).ShouldBe("2026-01-01 12:00 UTC+00");
    }

    [Test]
    public void Ago_WhenOldAndZoneGiven_ShouldShowDateInThatZone()
    {
        var value = new DateTimeOffset(2026, 1, 2, 20, 0, 0, TimeSpan.Zero);
        var now = value.AddDays(10);

        DisplayDate.Ago(value, now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok")).ShouldBe("2026-01-03");
        DisplayDate.Ago(value, now).ShouldBe("2026-01-02");
    }

    [Test]
    public void Day_WhenCalled_ShouldStayUtc()
    {
        DisplayDate.Day(new DateTimeOffset(2026, 1, 2, 20, 0, 0, TimeSpan.Zero)).ShouldBe("2 Jan 2026");
    }

    [TestCase(0, "just now")]
    [TestCase(59, "just now")]
    [TestCase(60, "1 min ago")]
    [TestCase(2 * 60 + 59, "2 min ago")]
    [TestCase(59 * 60 + 59, "59 min ago")]
    [TestCase(60 * 60, "1 h ago")]
    [TestCase(23 * 3600 + 3599, "23 h ago")]
    [TestCase(24 * 3600, "Yesterday")]
    [TestCase(47 * 3600 + 3599, "Yesterday")]
    [TestCase(48 * 3600, "2 days ago")]
    [TestCase(7 * 86400 - 1, "6 days ago")]
    [TestCase(7 * 86400, "2025-12-26")]
    [TestCase(-30, "just now")]
    public void Ago_WhenSecondsElapsed_ShouldReturnLabel(int seconds, string label)
    {
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        DisplayDate.Ago(now.AddSeconds(-seconds), now).ShouldBe(label);
    }
}
