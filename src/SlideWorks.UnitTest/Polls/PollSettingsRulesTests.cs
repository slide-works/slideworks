using SlideWorks.Core.Polls;

namespace SlideWorks.UnitTest.Polls;

[TestFixture]
public class PollSettingsRulesTests
{
    [Test]
    public void Check_WhenDefaults_ShouldPass()
    {
        PollSettingsRules.Check(PollSettings.Default, 2).ShouldBeNull();
    }

    [Test]
    public void Check_WhenUpToWithinTheOptions_ShouldPass()
    {
        var settings = PollSettings.Default with { Pick = PollPick.UpTo, Max = 4, Correct = 4 };

        PollSettingsRules.Check(settings, 4).ShouldBeNull();
    }

    [Test]
    public void Check_WhenMaxWithoutUpTo_ShouldNameMax()
    {
        var settings = PollSettings.Default with { Max = 2 };

        PollSettingsRules.Check(settings, 4).ShouldBe("max: only when pick is upTo");
    }

    [Test]
    public void Check_WhenMaxWithAny_ShouldNameMax()
    {
        var settings = PollSettings.Default with { Pick = PollPick.Any, Max = 2 };

        PollSettingsRules.Check(settings, 4).ShouldBe("max: only when pick is upTo");
    }

    [Test]
    public void Check_WhenAnyWithoutMax_ShouldPass()
    {
        var settings = PollSettings.Default with { Pick = PollPick.Any };

        PollSettingsRules.Check(settings, 4).ShouldBeNull();
    }

    [Test]
    public void Check_WhenUpToWithoutMax_ShouldNameMax()
    {
        var settings = PollSettings.Default with { Pick = PollPick.UpTo };

        PollSettingsRules.Check(settings, 4).ShouldBe("max: required when pick is upTo");
    }

    [Test]
    public void Check_WhenMaxAboveTheOptionCount_ShouldNameMax()
    {
        var settings = PollSettings.Default with { Pick = PollPick.UpTo, Max = 5 };

        PollSettingsRules.Check(settings, 4).ShouldBe("max: must be 2 to 4");
    }

    [Test]
    public void Check_WhenMaxIsOne_ShouldNameMax()
    {
        var settings = PollSettings.Default with { Pick = PollPick.UpTo, Max = 1 };

        PollSettingsRules.Check(settings, 4).ShouldBe("max: must be 2 to 4");
    }

    [Test]
    public void Check_WhenCorrectAboveTheOptionCount_ShouldNameCorrect()
    {
        var settings = PollSettings.Default with { Correct = 5 };

        PollSettingsRules.Check(settings, 4).ShouldBe("correct: must be 1 to 4");
    }

    [Test]
    public void Check_WhenCorrectIsZero_ShouldNameCorrect()
    {
        var settings = PollSettings.Default with { Correct = 0 };

        PollSettingsRules.Check(settings, 4).ShouldBe("correct: must be 1 to 4");
    }
}
