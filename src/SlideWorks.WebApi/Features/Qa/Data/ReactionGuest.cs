namespace SlideWorks.WebApi.Features.Qa.Data;

// That a guest reacted at least once in a session (Q7, D9, D19), for the measure, and nothing more: no count, no emoji, no
// time. Written by the guest's first tap in the session. GuestId never leaves the server. Never deleted. Only Features/Qa
// writes it.
public class ReactionGuest
{
    public Guid SessionId { get; set; }

    public required string GuestId { get; set; }
}
