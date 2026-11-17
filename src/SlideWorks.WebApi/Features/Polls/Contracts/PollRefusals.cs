namespace SlideWorks.WebApi.Features.Polls.Contracts;

// The codes a refused poll action gives (V4), each as a ConflictException and a 409 { "error": code }. poll-control.json
// holds a sample of each.
public static class PollRefusals
{
    // Open with no open audience session.
    public const string NOT_LIVE = "not_live";

    // The action isn't allowed in the round's phase.
    public const string WRONG_PHASE = "wrong_phase";

    // Open while another slide's round is open.
    public const string ANOTHER_POLL_OPEN = "another_poll_open";

    // Reveal while the room already sees the counts.
    public const string ALREADY_VISIBLE = "already_visible";

    // Show answer on a poll with no correct answer.
    public const string NO_CORRECT = "no_correct";
}
