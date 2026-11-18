using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// One template in GET /api/looks (looks.json). LockedPalette is the palette a locking template forces, else null. Ref
// is what a deck stores: a built-in's name or an uploaded template's tpl_… id. Version is the latest published version,
// null on a built-in. Owner is the name of the person who uploaded it, null on a built-in and on the caller's own.
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
    string? LockedPalette,
    string Ref,
    int? Version,
    bool BuiltIn,
    string? Owner)
{
    public static LooksTemplateView From(ResolvedTemplate resolved, Guid? callerId)
    {
        var template = resolved.Template;
        return new(
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
            template.LockPalette ? template.Palette : null,
            resolved.Ref,
            resolved.Version,
            resolved.BuiltIn,
            resolved.BuiltIn || resolved.OwnerId == callerId ? null : resolved.OwnerName);
    }
}
