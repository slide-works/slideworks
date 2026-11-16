using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide as get_deck returns it (K6, L7): with its revision, which update_slide needs, its deep_dive flag, and its
// notes, prefixed while the slide is a deep dive (D16).
public sealed record GetDeckSlide(
    int Number,
    string Layout,
    int? Part,
    [property: JsonPropertyName("deep_dive")] bool DeepDive,
    int Steps,
    long Revision,
    JsonElement Slots,
    string? Notes)
{
    public static GetDeckSlide From(Slide slide)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.DeepDive, slide.Steps, slide.Revision, SlideJson.Slots(slide), DeckNotes.Of(slide));
}
