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
