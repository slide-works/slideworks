using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// One template in GET /api/looks (looks.json). LockedPalette is the palette a locking template forces, else null.
public sealed record LooksTemplateView(
    string Name,
    string Label,
    string Purpose,
    bool IsDefault,
    IReadOnlyList<string> Layouts,
    TemplateChrome Chrome,
    IReadOnlyList<string> Outline,
    TemplateType Type,
    TemplateShape Shape,
    string Ornament,
    string? TitleArt,
    string? LockedPalette)
{
    public static LooksTemplateView From(Template template)
        => new(
            template.Name,
            template.Label,
            template.Purpose,
            template.IsDefault,
            template.Layouts,
            template.Chrome,
            template.Outline,
            template.Type,
            template.Shape,
            template.Ornament,
            template.TitleArt,
            template.LockPalette ? template.Palette : null);
}
