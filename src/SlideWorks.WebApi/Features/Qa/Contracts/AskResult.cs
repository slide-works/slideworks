namespace SlideWorks.WebApi.Features.Qa.Contracts;

// What IQuestionAsking.AskAsync returns (Q4), a closed set: the private constructor means only the cases below exist.
// QuestionResults.ToActionResult maps them to 201 AskedQuestion, 400 { error }, 409 { error }, 429 TooManyQuestions and 404
// no_session. Nothing is written for any case but Asked.
public abstract record AskResult
{
    private AskResult()
    {
    }

    // The question was stored, waiting or open, with the asker's upvote unless the writer refused it.
    public sealed record Asked(GuestQuestion Question, MyVotes Mine) : AskResult;

    // The text or name breaks a rule, named in Message by QuestionText, such as "text: at most 200".
    public sealed record Invalid(string Message) : AskResult;

    // The room can't ask now: Error is GuestQaErrors.QUESTIONS_CLOSED, QUESTIONS_OFF or QUESTIONS_OFF_HERE.
    public sealed record Refused(string Error) : AskResult;

    // The guest asked 3 questions in the last minute; RetryAfter is whole seconds, an integer.
    public sealed record Limited(int RetryAfter) : AskResult;

    // No live session has this code, or the guest isn't on its roster.
    public sealed record NoSession() : AskResult;
}
