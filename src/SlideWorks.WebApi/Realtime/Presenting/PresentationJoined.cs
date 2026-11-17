namespace SlideWorks.WebApi.Realtime.Presenting;

// JoinPresentation's result (P2): the state after the join, and minutes since local midnight in the caller's time zone
// (0-1439, from TimeProvider), which the client advances with performance.now() (D7).
public sealed record PresentationJoined(PresentationState State, int ClockMinutes);
