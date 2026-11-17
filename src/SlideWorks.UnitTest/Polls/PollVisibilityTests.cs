using SlideWorks.Core.Polls;

namespace SlideWorks.UnitTest.Polls;

// One test per row of V5's visibility rule.
[TestFixture]
public class PollVisibilityTests
{
    [Test]
    public void RoomSees_WhenResultsLive_ShouldBeTrue()
    {
        PollVisibility.RoomSees(PollResults.Live, revealed: false, closed: false).ShouldBeTrue();
    }

    [TestCase(PollResults.AfterAnswer)]
    [TestCase(PollResults.AfterClose)]
    public void RoomSees_WhenHiddenAndOpenAndNotRevealed_ShouldBeFalse(PollResults results)
    {
        PollVisibility.RoomSees(results, revealed: false, closed: false).ShouldBeFalse();
    }

    [TestCase(PollResults.AfterAnswer)]
    [TestCase(PollResults.AfterClose)]
    public void RoomSees_WhenRevealed_ShouldBeTrue(PollResults results)
    {
        PollVisibility.RoomSees(results, revealed: true, closed: false).ShouldBeTrue();
    }

    [TestCase(PollResults.AfterAnswer)]
    [TestCase(PollResults.AfterClose)]
    public void RoomSees_WhenClosed_ShouldBeTrue(PollResults results)
    {
        PollVisibility.RoomSees(results, revealed: false, closed: true).ShouldBeTrue();
    }

    [TestCase(PollResults.Live, false)]
    [TestCase(PollResults.AfterAnswer, false)]
    [TestCase(PollResults.AfterClose, false)]
    [TestCase(PollResults.AfterClose, true)]
    public void GuestSees_WhenTheRoomSees_ShouldBeTrue(PollResults results, bool hasAnswered)
    {
        PollVisibility.GuestSees(roomSees: true, results, hasAnswered).ShouldBeTrue();
    }

    [Test]
    public void GuestSees_WhenAfterAnswerAndAnswered_ShouldBeTrue()
    {
        PollVisibility.GuestSees(roomSees: false, PollResults.AfterAnswer, hasAnswered: true).ShouldBeTrue();
    }

    [Test]
    public void GuestSees_WhenAfterAnswerAndNotAnswered_ShouldBeFalse()
    {
        PollVisibility.GuestSees(roomSees: false, PollResults.AfterAnswer, hasAnswered: false).ShouldBeFalse();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GuestSees_WhenAfterCloseAndTheRoomDoesNot_ShouldBeFalse(bool hasAnswered)
    {
        PollVisibility.GuestSees(roomSees: false, PollResults.AfterClose, hasAnswered).ShouldBeFalse();
    }
}
