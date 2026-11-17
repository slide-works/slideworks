using System.Text.Json;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One slide in the play view (deck-play.json). Never carries notes (D16). DeepDive marks a deep dive (D4).
public sealed record DeckPlaySlide(int Number, string Layout, int? Part, bool DeepDive, int Steps, JsonElement Slots)
{
    public static DeckPlaySlide From(Slide slide)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.DeepDive, slide.Steps, SlideJson.Slots(slide));
}
