namespace SlideWorks.WebApi.Features.Slides;

// One entry of set_notes: the slide's 1-based number and its new notes. Null or blank text clears them.
public sealed record SlideNote(int Slide, string? Text);
