using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Data;

// One question of a Q&A round (Q2, D13), keyed by the round and its 0-based position: the same key as its PollOption, which
// holds its text and is written in the same transaction. Its number on the wire is Position + 1. State is waiting, open or
// answered; HiddenAt set means hidden, and the state under it is kept so Restore returns it. At most one question per round
// is OnScreen, which a partial unique index enforces. Name is at most 30 characters (a check constraint), null for an
// anonymous question. GuestId is the asker's sw_guest id and never leaves the server. Never deleted. Only Features/Qa
// writes it.
public class Question
{
    public Guid RoundId { get; set; }

    public int Position { get; set; }

    public required string GuestId { get; set; }

    public string? Name { get; set; }

    public QuestionState State { get; set; }

    public DateTimeOffset? HiddenAt { get; set; }

    public bool OnScreen { get; set; }

    public DateTimeOffset AskedAt { get; set; }

    public DateTimeOffset? AnsweredAt { get; set; }
}
