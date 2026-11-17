namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of PUT /api/decks/{id}/qa/slides/{slide} (qa-control.json "slideRequest", Q6) and IQaSettings.SetSlideAsync's
// change: either switch may be left out (null) to keep it.
public sealed record QaSlidePatch(bool? Questions, bool? Reactions);
