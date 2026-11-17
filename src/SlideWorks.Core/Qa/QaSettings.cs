namespace SlideWorks.Core.Qa;

// A deck's Q&A and reaction settings (Q1, D2 to D5). A deck with no settings row has Default. Review makes a new question
// wait for approval; Names false removes "Show my name" and refuses a name; Reactions false hides the reaction row.
public sealed record QaSettings(QaQuestions Questions, bool Review, bool Names, bool Reactions, QaShowOn ShowOn)
{
    // Questions open, no review, names allowed, reactions on, shown questions over any slide.
    public static QaSettings Default { get; } = new(QaQuestions.Open, false, true, true, QaShowOn.AnySlide);
}
