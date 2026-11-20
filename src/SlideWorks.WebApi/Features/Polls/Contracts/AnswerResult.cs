namespace SlideWorks.WebApi.Features.Polls.Contracts;

// What IPollAnswers.SubmitAsync returns (V5), a closed set: the private constructor means only the five cases below exist.
// AnswerResults.ToActionResult maps them to 200 MyAnswer, 409 StaleAnswer, 400 { error }, 429 too_many_answers and 404
// no_session.
public abstract record AnswerResult
{
    private AnswerResult()
    {
    }

    // The answer committed: its picks and its new revision.
    public sealed record Accepted(IReadOnlyList<int> Picks, int Revision, string? Text = null) : AnswerResult;

    // Nothing was written: the round isn't the open one, the revision doesn't match, or the poll isn't open.
    public sealed record Stale(GuestPollView Current) : AnswerResult;

    // The picks break a rule, named in Message, such as "picks: at most 2".
    public sealed record Invalid(string Message) : AnswerResult;

    // The guest sent more than 30 answers in a minute (D18).
    public sealed record Limited() : AnswerResult;

    // No live session has this code.
    public sealed record NoSession() : AnswerResult;
}
