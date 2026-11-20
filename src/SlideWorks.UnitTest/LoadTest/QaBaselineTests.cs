using SlideWorks.LoadTest;

namespace SlideWorks.UnitTest.LoadTest;

[TestFixture]
public class QaBaselineTests
{
    private static QaBaseline Baseline() => new("round-1", new Dictionary<int, int> { [1] = 4, [2] = 0 });

    [Test]
    public void Acknowledge_WhenQuestionKnown_ShouldAddItsBaselineVotesAndTheOrdinal()
    {
        var baseline = Baseline();

        baseline.Acknowledge(1).ShouldBe(5);
        baseline.Acknowledge(1).ShouldBe(6);
        baseline.Acknowledge(1).ShouldBe(7);
    }

    [Test]
    public void Acknowledge_WhenTwoQuestions_ShouldCountEachOnItsOwn()
    {
        var baseline = Baseline();

        baseline.Acknowledge(1).ShouldBe(5);
        baseline.Acknowledge(2).ShouldBe(1);
        baseline.Acknowledge(1).ShouldBe(6);
        baseline.Acknowledge(2).ShouldBe(2);
    }

    [Test]
    public void Acknowledge_WhenQuestionWasUnknownAtPrepare_ShouldNotThrowAndCountFromZero()
    {
        var baseline = Baseline();

        baseline.Acknowledge(271).ShouldBe(1);
        baseline.Acknowledge(271).ShouldBe(2);
    }

    [Test]
    public void Open_ShouldListTheQuestionsOfTheBaseline()
    {
        Baseline().Open.OrderBy(n => n).ShouldBe([1, 2]);
    }
}
