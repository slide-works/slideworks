namespace SlideWorks.WebApi.Realtime;

// One audience connection on the roster (SW-883 D11, D12): the language its guest picked (null until SetLanguage) and the
// language of the slide-state group it is in (null until it is placed in one).
public sealed record AudienceSeat(string ConnectionId, string? Chosen, string? Placed);
