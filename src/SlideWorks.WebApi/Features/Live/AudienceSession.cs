namespace SlideWorks.WebApi.Features.Live;

// One run of a deck in front of an audience (J2): Go live opens it, End closes it. A deck has at most one open session
// (EndedAt is null), and its guests' joins sit under it, so answers stay separate per run.
public class AudienceSession
{
    public Guid Id { get; set; }

    public required string DeckId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    // The most guests joined at once during the session.
    public int PeakJoined { get; set; }

    // The first time the live state landed on a join slide, for the seconds-to-join measure (J8).
    public DateTimeOffset? JoinSlideFirstShownAt { get; set; }
}
