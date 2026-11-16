namespace SlideWorks.WebApi.Features.Slides;

// update_slide: { slide, steps, revision, url }, the same shape as add_slide's. revision is the one the next update needs.
public sealed record UpdateSlideResult(int Slide, int Steps, long Revision, string Url);
