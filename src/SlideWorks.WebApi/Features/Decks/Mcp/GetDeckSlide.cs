using System.Text.Json;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide as get_deck returns it (K6): with its revision, which update_slide needs, and its notes.
public sealed record GetDeckSlide(int Number, string Layout, int? Part, int Steps, long Revision, JsonElement Slots, string? Notes)
{
    public static GetDeckSlide From(Slide slide)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.Steps, slide.Revision, SlideJson.Slots(slide), slide.Notes);
}
