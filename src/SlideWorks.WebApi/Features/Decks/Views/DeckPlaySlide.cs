using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One slide in the play view (deck-play.json). Never carries notes (D16). DeepDive marks a deep dive (D4). Surface is
// the surface the slide is drawn on, "light" or "dark", never null (K6).
public sealed record DeckPlaySlide(int Number, string Layout, int? Part, bool DeepDive, string Surface, int Steps, JsonElement Slots)
{
    public static DeckPlaySlide From(Slide slide, ResolvedTemplate template)
        => new(slide.Position + 1, slide.Layout, slide.Part, slide.DeepDive, SurfaceOf(slide, template), slide.Steps, SlideJson.Slots(slide));

    // The slide's resolved surface, for every slide view (K6): the one the slide stores, else its layout's own as the
    // deck's template finds the layout. A layout that takes either surface, or one the template does not know, is light.
    public static string SurfaceOf(Slide slide, ResolvedTemplate template)
        => slide.Surface ?? (template.FindLayout(slide.Layout)?.Dark == true ? SlideSurface.DARK : SlideSurface.LIGHT);
}
