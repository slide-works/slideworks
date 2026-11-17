namespace SlideWorks.WebApi.Features.Qa.Contracts;

// What IReactions.TapAsync returns (Q7), a closed set: the private constructor means only the cases below exist.
// QuestionResults.ToActionResult maps them to 204, 400 { error }, 409 { error }, 429 too_many_reactions and 404 no_session.
public abstract record ReactionResult
{
    private ReactionResult()
    {
    }

    // The tap was counted against the slide on screen.
    public sealed record Counted() : ReactionResult;

    // The emoji isn't one of the five: Message is GuestQaErrors.UNKNOWN_EMOJI.
    public sealed record Invalid(string Message) : ReactionResult;

    // The room's reactions are off on this slide or deck: Error is GuestQaErrors.REACTIONS_OFF.
    public sealed record Refused(string Error) : ReactionResult;

    // The guest tapped 10 times in the last 5 seconds.
    public sealed record Limited() : ReactionResult;

    // No live session has this code, or the guest isn't on its roster.
    public sealed record NoSession() : ReactionResult;
}
