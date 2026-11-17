using SlideWorks.Core.Qa;

namespace SlideWorks.UnitTest.Qa;

[TestFixture]
public class QaRoomRulesTests
{
    private static readonly QaRoundState _live = new("0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10", 12);

    [Test]
    public void For_WhenDefaultsAndNoSwitch_ShouldBeOpenWithTheRound()
    {
        var room = QaRoomRules.For(QaSettings.Default, QaSlideSwitch.On, _live);

        room.ShouldBe(new QaRoom(_live.Round, QaRoomQuestions.Open, true, true, 12));
    }

    [Test]
    public void For_WhenSlideSwitchOffAndDeckOpen_ShouldBeOffHere()
    {
        var room = QaRoomRules.For(QaSettings.Default, new QaSlideSwitch(false, true), _live);

        room.Questions.ShouldBe(QaRoomQuestions.OffHere);
        room.Round.ShouldBe(_live.Round);
        room.Reactions.ShouldBeTrue();
    }

    [Test]
    public void For_WhenDeckClosed_ShouldBeClosedWhateverTheSlide()
    {
        var settings = QaSettings.Default with { Questions = QaQuestions.Closed };

        QaRoomRules.For(settings, new QaSlideSwitch(false, false), _live).Questions.ShouldBe(QaRoomQuestions.Closed);
        QaRoomRules.For(settings, QaSlideSwitch.On, _live).Questions.ShouldBe(QaRoomQuestions.Closed);
    }

    [Test]
    public void For_WhenDeckOff_ShouldHaveNoRound()
    {
        var settings = QaSettings.Default with { Questions = QaQuestions.Off };

        var room = QaRoomRules.For(settings, new QaSlideSwitch(false, true), _live);

        room.Questions.ShouldBe(QaRoomQuestions.Off);
        room.Round.ShouldBeNull();
        room.ListRevision.ShouldBe(0);
    }

    [Test]
    public void For_WhenNoRoundIsOpen_ShouldGiveListRevisionZero()
    {
        var room = QaRoomRules.For(QaSettings.Default, QaSlideSwitch.On, new QaRoundState(null, 12));

        room.Round.ShouldBeNull();
        room.ListRevision.ShouldBe(0);
        room.Questions.ShouldBe(QaRoomQuestions.Open);
    }

    [Test]
    public void For_WhenDeckReactionsOff_ShouldIgnoreTheSlideSwitch()
    {
        var settings = QaSettings.Default with { Reactions = false };

        QaRoomRules.For(settings, QaSlideSwitch.On, _live).Reactions.ShouldBeFalse();
        QaRoomRules.For(settings, new QaSlideSwitch(true, false), _live).Reactions.ShouldBeFalse();
    }

    [Test]
    public void For_WhenSlideSwitchesReactionsOff_ShouldTurnThemOffHere()
    {
        var room = QaRoomRules.For(QaSettings.Default, new QaSlideSwitch(true, false), _live);

        room.Reactions.ShouldBeFalse();
        room.Questions.ShouldBe(QaRoomQuestions.Open);
    }

    [Test]
    public void For_WhenNamesOff_ShouldPassNamesOff()
    {
        QaRoomRules.For(QaSettings.Default with { Names = false }, QaSlideSwitch.On, _live).Names.ShouldBeFalse();
    }
}
