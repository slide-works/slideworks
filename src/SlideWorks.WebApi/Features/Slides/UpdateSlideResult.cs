namespace SlideWorks.WebApi.Features.Slides;

// update_slide: { slide, steps, revision, url, warnings }, the same shape as add_slide's. revision is the one the next
// update needs; warnings says when the slide's layout is outside the deck's template (D9).
public sealed record UpdateSlideResult(int Slide, int Steps, long Revision, string Url, IReadOnlyList<string> Warnings);
