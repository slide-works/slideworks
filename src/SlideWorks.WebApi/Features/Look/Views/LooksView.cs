using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// GET /api/looks (looks.json): the templates the caller may pick, each at its latest version, and the built-in palettes,
// for the New deck dialog and the Look panel (D20). callerId is the caller's own id, so their own templates name no owner.
public sealed record LooksView(IReadOnlyList<LooksTemplateView> Templates, IReadOnlyList<LooksPaletteView> Palettes)
{
    public static LooksView From(IReadOnlyList<ResolvedTemplate> templates, IPaletteCatalogue palettes, Guid? callerId)
        => new(templates.Select(t => LooksTemplateView.From(t, callerId)).ToList(), palettes.All.Select(LooksPaletteView.From).ToList());
}
