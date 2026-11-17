using SlideWorks.Core.Qa;

namespace SlideWorks.UnitTest.Qa;

[TestFixture]
public class QuestionTextTests
{
    [Test]
    public void Check_WhenTextIsOnlySpaces_ShouldBeRequired()
    {
        QuestionText.Check(" \t\n  ", null, true).ShouldBe("text: required");
    }

    [Test]
    public void Check_WhenTextIsNull_ShouldBeRequired()
    {
        QuestionText.Check(null, null, true).ShouldBe("text: required");
    }

    [Test]
    public void Check_WhenTextIs200CharactersAfterCleaning_ShouldPass()
    {
        QuestionText.Check("  " + new string('a', 200) + "  ", null, true).ShouldBeNull();
    }

    [Test]
    public void Check_WhenTextIs201Characters_ShouldBeRefused()
    {
        QuestionText.Check(new string('a', 201), null, true).ShouldBe("text: at most 200");
    }

    [Test]
    public void Check_WhenNameIs31Characters_ShouldBeRefused()
    {
        QuestionText.Check("How do you split a big change?", new string('n', 31), true).ShouldBe("name: at most 30");
    }

    [Test]
    public void Check_WhenNameIs30Characters_ShouldPass()
    {
        QuestionText.Check("How do you split a big change?", new string('n', 30), true).ShouldBeNull();
    }

    [Test]
    public void Check_WhenNamesOffAndANameIsGiven_ShouldBeRefused()
    {
        QuestionText.Check("How do you split a big change?", "Priya", false).ShouldBe("name: not allowed");
    }

    [Test]
    public void Check_WhenNamesOffAndTheNameIsBlank_ShouldPass()
    {
        QuestionText.Check("How do you split a big change?", "   ", false).ShouldBeNull();
    }

    [Test]
    public void Check_WhenTextAndNameAreBothWrong_ShouldNameTheTextFirst()
    {
        QuestionText.Check("", new string('n', 31), false).ShouldBe("text: required");
    }

    [Test]
    public void Text_WhenRunsOfWhitespace_ShouldTrimAndMakeEachOneSpace()
    {
        QuestionText.Text("  How do\n\nyou   split\ta big change?  ").ShouldBe("How do you split a big change?");
    }

    [Test]
    public void Name_WhenBlank_ShouldBeNull()
    {
        QuestionText.Name(" \n ").ShouldBeNull();
        QuestionText.Name(null).ShouldBeNull();
    }

    [Test]
    public void Name_WhenGiven_ShouldTrimAndCollapse()
    {
        QuestionText.Name("  Priya   S ").ShouldBe("Priya S");
    }
}
