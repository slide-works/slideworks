namespace SlideWorks.WebApi.Features.Qa.Contracts;

// What IGuestQuestions.VoteAsync returns (Q4), a closed set: the private constructor means only the cases below exist.
// QuestionResults.ToActionResult maps them to 200 MyVotes, 409 StaleVotes, 400 { error }, 409 { error }, 429
// too_many_votes and 404 no_session. Nothing is written for any case but Voted.
public abstract record VoteResult
{
    private VoteResult()
    {
    }

    // The votes were stored: the picks kept and applied, and the new revision.
    public sealed record Voted(IReadOnlyList<int> Picks, int Revision) : VoteResult;

    // The round isn't the open Q&A round or the revision doesn't match; Current is what the phone should show now.
    public sealed record Stale(GuestQuestionsView Current) : VoteResult;

    // The picks break a rule, named in Message, such as "picks: unknown question 7" or "picks: duplicate".
    public sealed record Invalid(string Message) : VoteResult;

    // The deck has questions off, so there is no round: Error is GuestQaErrors.QUESTIONS_OFF.
    public sealed record Refused(string Error) : VoteResult;

    // The guest changed its votes 30 times in the last minute.
    public sealed record Limited() : VoteResult;

    // No live session has this code, or the guest isn't on its roster.
    public sealed record NoSession() : VoteResult;
}
