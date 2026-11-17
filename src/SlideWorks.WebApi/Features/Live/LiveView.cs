namespace SlideWorks.WebApi.Features.Live;

// The deck's live state for its owner (live.json, J2). SessionId is the open session's id and null when the deck is not live.
// JoinedCount is how many guests are connected now, and JoinBar whether the deck's join bar is on.
public sealed record LiveView(string? SessionId, string Code, string CodeLabel, string JoinUrl, string HostLabel, bool Live, int JoinedCount, bool JoinBar);
