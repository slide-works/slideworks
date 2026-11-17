namespace SlideWorks.WebApi.Features.Polls.Data;

// One option of a Q&A round (SW-9 Q2): a question, at its 0-based position in the round, keyed by the round and the
// position. Text is at most 200 characters, which a check constraint enforces. Rows exist only for a round whose poll is
// Qa; a slide poll's options stay in its slide's slots (SW-6 D13). Never deleted. Only Features/Polls and Features/Qa
// write them.
public class PollOption
{
    public const int TEXT_MAX = 200;

    public Guid RoundId { get; set; }

    public int Position { get; set; }

    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
