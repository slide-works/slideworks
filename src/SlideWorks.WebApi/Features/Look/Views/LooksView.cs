using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// GET /api/looks (looks.json): the static catalogue the New deck dialog and the Look panel read (D20).
public sealed record LooksView(IReadOnlyList<LooksTemplateView> Templates, IReadOnlyList<LooksPaletteView> Palettes)
{
    public static LooksView From(ITemplateCatalogue templates, IPaletteCatalogue palettes)
        => new(templates.All.Select(LooksTemplateView.From).ToList(), palettes.All.Select(LooksPaletteView.From).ToList());
}
