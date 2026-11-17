namespace SlideWorks.WebApi.Features.Polls;

// A guest of a known join code (GuestPollViews.SeatAsync): the deck, and while the deck is live its open session and when
// this guest first joined it. SessionId and JoinedAt are null when the deck is not live.
public sealed record GuestSeat(string DeckId, Guid? SessionId, DateTimeOffset? JoinedAt);
