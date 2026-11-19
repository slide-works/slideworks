namespace SlideWorks.WebApi.Features.Slides;

// add_slides: every slide in the batch, in the order given, whether or not it was stored.
public sealed record AddSlidesResult(long Revision, int Added, IReadOnlyList<AddSlidesItemResult> Slides);
