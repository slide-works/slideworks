namespace SlideWorks.WebApi.Features.Polls.Data;

// One round of a poll slide in one audience session (V2, D7). Number starts at 1 per slide per session. A round with no
// OpenedAt is a draft (a new round on a manual poll, not opened yet); with OpenedAt and no ClosedAt it is open; with
// ClosedAt it is closed, and Final and JoinedAtClose are written in the same transaction. A session has at most one round
// without ClosedAt (draft or open), which a partial unique index enforces. Rounds are closed, never deleted. Only
// Features/Polls writes them.
public class PollRound
{
    public Guid Id { get; set; }

    public Guid SlideId { get; set; }

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
