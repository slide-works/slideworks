using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks;

// A slide's notes as the owner reads them (D16): stored notes never change, and a deep dive's notes read with the skip
// prefix while the flag is set, so clearing the flag removes it. The play view never carries notes.
public static class DeckNotes
{
    public const string DEEP_DIVE_PREFIX = "Deep dive. Skip if short of time.";

    public static string? Of(Slide slide)
    {
        if (!slide.DeepDive)
        {
            return slide.Notes;
        }
        return string.IsNullOrEmpty(slide.Notes) ? DEEP_DIVE_PREFIX : $"{DEEP_DIVE_PREFIX} {slide.Notes}";
    }
}
