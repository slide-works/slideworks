namespace SlideWorks.WebApi.Realtime.Presenting;

// UpdatePresentation's argument (P2): the position and switches a client asks for. The server clamps the position to the
// deck's current slides and steps; last write wins.
public sealed record PresentationUpdate(int Slide, int Step, bool Black, bool SkipDeepDives);
