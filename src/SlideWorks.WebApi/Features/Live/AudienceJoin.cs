namespace SlideWorks.WebApi.Features.Live;

// One row per guest per session (J2): when that guest first joined it.
public class AudienceJoin
{
    public Guid SessionId { get; set; }

    // The guest's sw_guest id.
    public required string GuestId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
