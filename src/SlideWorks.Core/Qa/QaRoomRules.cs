namespace SlideWorks.Core.Qa;

// The one place the effective Q&A values for the slide on screen are worked out (Q1). Closed wins over the slide's switch,
// and a switch can only turn off what the deck has on.
public static class QaRoomRules
{
    public static QaRoom For(QaSettings settings, QaSlideSwitch slide, QaRoundState live)
    {
        var questions = settings.Questions switch
        {
            QaQuestions.Off => QaRoomQuestions.Off,
            QaQuestions.Closed => QaRoomQuestions.Closed,
            _ => slide.Questions ? QaRoomQuestions.Open : QaRoomQuestions.OffHere,
        };
        var round = questions == QaRoomQuestions.Off ? null : live.Round;
        return new QaRoom(round, questions, settings.Names, settings.Reactions && slide.Reactions, live.ListRevision);
    }
}
