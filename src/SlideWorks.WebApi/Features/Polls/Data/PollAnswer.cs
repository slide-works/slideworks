namespace SlideWorks.WebApi.Features.Polls.Data;

// One guest's answer in one round (V2), keyed by the round and the guest's sw_guest id. Picks are 0-based option
// positions, ascending. Revision starts at 1 and goes up by one on each replace. An answer is replaced, never deleted, and
// is read only to replace it, to return it to its own guest and to rebuild the tally: never listed by who picked what (D5).
// Only Features/Polls writes it.
public class PollAnswer
{
    public Guid RoundId { get; set; }

    public required string GuestId { get; set; }

    public required IReadOnlyList<int> Picks { get; set; }

    // The guest's own words on a text or word-cloud question, or null on a choice poll.
    public string? Text { get; set; }

    public int Revision { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
