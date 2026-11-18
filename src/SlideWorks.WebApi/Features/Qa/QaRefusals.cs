namespace SlideWorks.WebApi.Features.Qa;

// The codes a refused moderation action gives (Q6, D13), each as a ConflictException and a 409 { "error": code }.
// qa-control.json holds a sample of each. An unknown question number is a 404 instead (NO_QUESTION).
public static class QaRefusals
{
    // A question action while the deck has no live audience session.
    public const string NOT_LIVE = "not_live";

    // The action isn't allowed in the question's state (waiting, open, answered, hidden, on screen).
    public const string WRONG_STATE = "wrong_state";

    // The 404 message for a number the open Q&A round has no question at, or a live deck with no open Q&A round.
    public const string NO_QUESTION = "The open Q&A round has no question with that number";
}
