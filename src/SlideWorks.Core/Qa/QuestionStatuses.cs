namespace SlideWorks.Core.Qa;

// The one mapping from a question's stored state and its hidden flag to the single QuestionStatus a guest's own question
// (Q4) and a questions report (Q10) carry: hidden wins over the state under it, which Restore brings back.
public static class QuestionStatuses
{
    public static QuestionStatus Of(QuestionState state, bool hidden) => hidden
        ? QuestionStatus.Hidden
        : state switch
        {
            QuestionState.Waiting => QuestionStatus.Waiting,
            QuestionState.Open => QuestionStatus.Open,
            QuestionState.Answered => QuestionStatus.Answered,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown question state."),
        };
}
