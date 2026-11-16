namespace SlideWorks.WebApi.Features.Slides;

// delete_slide: the number the deleted slide had, how many live slides are left, and the deck revision every renumbered
// slide now carries.
public sealed record DeleteSlideResult(int Deleted, int Slides, long Revision);
