namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The error codes the guest's Q&A and reaction routes give (Q4, Q7), each as { "error": code }. guest-questions.json,
// question-ask.json and question-votes.json hold a sample of each. SW-6's no_guest, no_session and too_many_attempts apply
// as they do to the poll routes.
public static class GuestQaErrors
{
    // 409: the deck's questions are closed (asking only; upvotes still count).
    public const string QUESTIONS_CLOSED = "questions_closed";

    // 409: the deck has questions off, so there is no round.
    public const string QUESTIONS_OFF = "questions_off";

    // 409: the slide on screen has questions switched off (asking only; upvotes still count).
    public const string QUESTIONS_OFF_HERE = "questions_off_here";

    // 409: the room's reactions are off.
    public const string REACTIONS_OFF = "reactions_off";

    // 409: the round isn't the open one or the revision doesn't match (StaleVotes).
    public const string STALE = "stale";

    // 429: more than 3 questions a minute (TooManyQuestions).
    public const string TOO_MANY_QUESTIONS = "too_many_questions";

    // 429: more than 30 vote changes a minute.
    public const string TOO_MANY_VOTES = "too_many_votes";

    // 429: more than 10 reaction taps in 5 seconds.
    public const string TOO_MANY_REACTIONS = "too_many_reactions";

    // 400: the emoji isn't one of the five.
    public const string UNKNOWN_EMOJI = "emoji: unknown";
}
