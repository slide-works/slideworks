using SlideWorks.Core.Qa;

namespace SlideWorks.UnitTest.Qa;

// Pure formatting: the times never meet a real clock.
[TestFixture]
public class QuestionAgeTests
{
    private static readonly DateTimeOffset _asked = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Label_WhenUnderAMinute_ShouldBeNow()
    {
        QuestionAge.Label(_asked, _asked).ShouldBe("now");
        QuestionAge.Label(_asked, _asked.AddSeconds(59)).ShouldBe("now");
    }

    [TestCase(60, "1 min")]
    [TestCase(2 * 60 + 59, "2 min")]
    [TestCase(59 * 60 + 59, "59 min")]
    [TestCase(60 * 60, "1 h")]
    [TestCase(2 * 3600 + 3599, "2 h")]
    public void Label_WhenOlder_ShouldCountWholeMinutesThenHours(int seconds, string label)
    {
        QuestionAge.Label(_asked, _asked.AddSeconds(seconds)).ShouldBe(label);
    }

    [Test]
    public void Label_WhenAskedAfterNow_ShouldBeNow()
    {
        QuestionAge.Label(_asked.AddSeconds(5), _asked).ShouldBe("now");
    }
}
