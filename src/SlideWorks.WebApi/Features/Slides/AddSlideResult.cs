namespace SlideWorks.WebApi.Features.Slides;

// add_slide: { slide, steps, revision, url } (K6). slide is the new slide's number, revision the one update_slide needs,
// and url the absolute player link to the slide.
public sealed record AddSlideResult(int Slide, int Steps, long Revision, string Url);
