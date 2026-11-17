using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Data;

// One round of a poll in one audience session (V2, D7; SW-9 Q2). Number starts at 1 per poll per session. A round with no
// OpenedAt is a draft (a new round on a manual poll, not opened yet); with OpenedAt and no ClosedAt it is open; with
// ClosedAt it is closed, and Final and JoinedAtClose are written in the same transaction. A session has at most one round
// of each kind without ClosedAt (draft or open), which a partial unique index enforces. Rounds are closed, never deleted.
// Only Features/Polls and Features/Qa write them.
public class PollRound
{
    public Guid Id { get; set; }

    public Guid PollId { get; set; }

    // Copied from the round's poll.
    public PollKind Kind { get; set; }

    // The poll slide, for a slide round; null for a Q&A round.
    public Guid? SlideId { get; set; }

    public Guid SessionId { get; set; }

    public int Number { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? RevealedAt { get; set; }

    public DateTimeOffset? AnswerShownAt { get; set; }

    // How many guests had joined the session when the round closed, for the response rate (V10).
    public int? JoinedAtClose { get; set; }

    public PollFinal? Final { get; set; }
}
