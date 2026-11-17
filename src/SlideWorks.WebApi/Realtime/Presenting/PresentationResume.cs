namespace SlideWorks.WebApi.Realtime.Presenting;

// JoinPresentation's resume argument (P2, D9): the client's last applied state, used only when the server has no state
// for the deck (after a restart or the 2-hour drop). The position is clamped and the timer kept as sent.
public sealed record PresentationResume(int Slide, int Step, bool Black, bool SkipDeepDives, PresentationTimer Timer);
