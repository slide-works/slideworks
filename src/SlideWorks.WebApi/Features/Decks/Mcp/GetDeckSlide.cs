using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide as get_deck returns it (K6, L7): with its revision, which update_slide needs, its deep_dive flag, and its
// notes, prefixed while the slide is a deep dive (D16). Surface is the surface the slide is drawn on, "light" or "dark".
// Lines is each line's state in the language get_deck was asked for (SW-883 K4), left out when no translated language was asked.
public sealed record GetDeckSlide(
    int Number,
    string Layout,
    int? Part,
    [property: JsonPropertyName("deep_dive")] bool DeepDive,
    string Surface,
    int Steps,
    long Revision,
    JsonElement Slots,
    string? Notes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LineView>? Lines = null)
{
    public static GetDeckSlide From(Slide slide, ResolvedTemplate template, IReadOnlyList<LineView>? lines)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.DeepDive, DeckPlaySlide.SurfaceOf(slide, template), slide.Steps, slide.Revision, SlideJson.Slots(slide), DeckNotes.Of(slide), lines);
}
