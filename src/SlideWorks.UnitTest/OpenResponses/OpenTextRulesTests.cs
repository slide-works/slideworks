using SlideWorks.Core.OpenResponses;

namespace SlideWorks.UnitTest.OpenResponses;

[TestFixture]
public class OpenTextRulesTests
{
    [Test]
    public void Check_WhenAListedWordIsSent_ShouldReturnProfane()
    {
        OpenTextRules.Check(["Teamwork", "  SHIT! "]).ShouldBe(OpenTextRules.PROFANE);
        OpenTextRules.Check("Ship it, you bastard.").ShouldBe(OpenTextRules.PROFANE);
        OpenTextRules.Check(["Scunthorpe", "class"]).ShouldBeNull();
    }

    [Test]
    public void Check_WhenTheTextIsEmpty_ShouldReturnRequired()
    {
        OpenTextRules.Check(Array.Empty<string>()).ShouldBe(OpenTextRules.REQUIRED);
        OpenTextRules.Check(["", "   "]).ShouldBe(OpenTextRules.REQUIRED);
        OpenTextRules.Check(" \t ").ShouldBe(OpenTextRules.REQUIRED);
    }

    [Test]
    public void Check_WhenOnlyCaseDiffers_ShouldBeTheSameWord()
    {
        string[] words = ["Team", " team ", "TEAM", "Big   ship", "big ship"];

        OpenTextRules.Check(words).ShouldBeNull();
        OpenTextRules.WordsOf(words).ShouldBe(["Team", "Big ship"]);
        OpenTextRules.KeyOf("  Big \t SHIP ").ShouldBe(OpenTextRules.KeyOf("big ship"));
    }

    [Test]
    public void Check_WhenFourWordsAreSent_ShouldReturnTooManyWords()
    {
        OpenTextRules.Check(["one", "two", "three", "four"]).ShouldBe(OpenTextRules.TOO_MANY_WORDS);
        OpenTextRules.Check(["one", "two", "three"]).ShouldBeNull();
    }

    [Test]
    public void Check_WhenTheWordIsTooLong_ShouldReturnWordTooLong()
    {
        OpenTextRules.Check(["ok", new string('w', 26)]).ShouldBe(OpenTextRules.WORD_TOO_LONG);
        OpenTextRules.Check([new string('w', 25)]).ShouldBeNull();
    }

    [Test]
    public void Check_WhenTheAnswerIsTooLong_ShouldReturnTextTooLong()
    {
        OpenTextRules.Check(new string('a', 201)).ShouldBe(OpenTextRules.TEXT_TOO_LONG);
        OpenTextRules.Check(new string('a', 200)).ShouldBeNull();
    }
}
