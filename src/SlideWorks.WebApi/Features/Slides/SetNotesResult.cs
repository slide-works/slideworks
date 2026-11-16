namespace SlideWorks.WebApi.Features.Slides;

// set_notes: the numbers of the slides whose notes were set, in the order given, and the revision they now carry.
public sealed record SetNotesResult(IReadOnlyList<int> Slides, long Revision);
