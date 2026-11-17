namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One slide's switch as the presenter sees it (qa-control.json "here" and "slides[]", Q6): the 1-based slide number, an
// integer, and whether asking and reactions are on while it is on screen.
public sealed record QaSlideView(int Slide, bool Questions, bool Reactions);
