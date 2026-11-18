using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide as get_deck returns it (K6, L7): with its revision, which update_slide needs, its deep_dive flag, and its
// notes, prefixed while the slide is a deep dive (D16). Surface is the surface the slide is drawn on, "light" or "dark".
public sealed record GetDeckSlide(
    int Number,
    string Layout,
    int? Part,
    [property: JsonPropertyName("deep_dive")] bool DeepDive,
    string Surface,
    int Steps,
    long Revision,
    JsonElement Slots,
    string? Notes)
{
    public static GetDeckSlide From(Slide slide, ResolvedTemplate template)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.DeepDive, DeckPlaySlide.SurfaceOf(slide, template), slide.Steps, slide.Revision, SlideJson.Slots(slide), DeckNotes.Of(slide));
}
