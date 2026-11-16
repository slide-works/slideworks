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
}
