namespace SlideWorks.WebApi.Features.Slides;

// move_slide: the moved slide's new number, the deck revision every renumbered slide now carries, and the slide's url.
public sealed record MoveSlideResult(int Slide, long Revision, string Url);
