using SlideWorks.Core.Qa;

namespace SlideWorks.UnitTest.Qa;

[TestFixture]
public class QuestionStatusesTests
{
    [TestCase(QuestionState.Waiting, QuestionStatus.Waiting)]
    [TestCase(QuestionState.Open, QuestionStatus.Open)]
    [TestCase(QuestionState.Answered, QuestionStatus.Answered)]
    public void Of_WhenNotHidden_ShouldBeTheState(QuestionState state, QuestionStatus status)
    {
        QuestionStatuses.Of(state, false).ShouldBe(status);
    }

    [TestCase(QuestionState.Waiting)]
    [TestCase(QuestionState.Open)]
    [TestCase(QuestionState.Answered)]
    public void Of_WhenHidden_ShouldBeHiddenWhateverTheState(QuestionState state)
    {
        QuestionStatuses.Of(state, true).ShouldBe(QuestionStatus.Hidden);
    }
}
