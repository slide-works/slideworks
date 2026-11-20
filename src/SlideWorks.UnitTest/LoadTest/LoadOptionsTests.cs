using SlideWorks.LoadTest;

namespace SlideWorks.UnitTest.LoadTest;

[TestFixture]
public class LoadOptionsTests
{
    [Test]
    public void Parse_WhenOnlyCode_ShouldBePollWithDefaults()
    {
        var options = LoadOptions.Parse(["--code", " 123456 "]);

        options.Code.ShouldBe("123456");
        options.Scenario.ShouldBe("poll");
        options.Voters.ShouldBe(100);
        options.Seconds.ShouldBe(10);
        options.ResultsTimeoutSeconds.ShouldBe(10);
        options.NoResults.ShouldBeFalse();
        options.Url.ShouldBe(new Uri("http://localhost:5510"));
        options.HubUrls.ShouldBe([new Uri("http://localhost:5510")]);
    }

    [Test]
    public void Parse_WhenQaWithGuestsQuestionsAndNoResults_ShouldReadThem()
    {
        var options = LoadOptions.Parse(["--code", "1", "--scenario", "qa", "--guests", "20", "--questions", "0", "--seconds", "5", "--no-results"]);

        options.Scenario.ShouldBe("qa");
        options.Voters.ShouldBe(20);
        options.Questions.ShouldBe(0);
        options.Seconds.ShouldBe(5);
        options.NoResults.ShouldBeTrue();
    }

    [Test]
    public void Parse_WhenQaWithoutQuestions_ShouldAskFive()
    {
        LoadOptions.Parse(["--code", "1", "--scenario", "qa"]).Questions.ShouldBe(5);
    }

    [TestCase("--code 1 --scenario bogus")]
    [TestCase("--code 1 --scenario QA")]
    [TestCase("--code 1 --scenario qa --pick 1")]
    [TestCase("--code 1 --scenario qa --owner-cookie a --deck d --slide 2")]
    [TestCase("--code 1 --scenario qa --close-at 3")]
    [TestCase("--code 1 --questions 3")]
    [TestCase("--code 1 --scenario qa --questions -1")]
    [TestCase("--code 1 --scenario qa --questions abc")]
    [TestCase("--code 1 --scenario qa --questions 1.5")]
    [TestCase("--code 1 --guests 0")]
    [TestCase("--code 1 --voters 5 --guests 5")]
    [TestCase("--code 1 --guests")]
    [TestCase("--voters 5")]
    [TestCase("--code 1 --seconds 0")]
    [TestCase("--code 1 --url ftp://x")]
    [TestCase("--code 1 --nonsense 1")]
    [TestCase("--code 1 --owner-cookie a")]
    public void Parse_WhenOptionsAreBad_ShouldThrowArgumentException(string line)
    {
        Should.Throw<ArgumentException>(() => LoadOptions.Parse(line.Split(' ')));
    }
}
