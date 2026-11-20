using SlideWorks.Core.Polls;

namespace SlideWorks.UnitTest.Polls;

[TestFixture]
public class OpenAnswersTests
{
    [Test]
    public void Words_WhenRepliesRepeat_ShouldCountAndSkipShortWords()
    {
        var words = OpenAnswers.Words(["Small batches ship", "small batches again", "A"]);

        words.Select(word => (word.Text, word.Count)).ShouldBe([("batches", 2), ("small", 2), ("again", 1), ("ship", 1)]);
    }

    [Test]
    public void Problem_WhenTextIsBlank_ShouldRefuse()
    {
        OpenAnswers.Problem("   ").ShouldBe("text: required");
    }
}
