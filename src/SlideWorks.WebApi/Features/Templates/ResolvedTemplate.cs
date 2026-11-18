using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Templates;

// A template as ITemplates answers it (T4). Ref is a built-in's name or an uploaded template's tpl_… id. Version and
// LatestVersion are null on a built-in. A template that brings its own palette has Template.Palette == "template" and
// carries it in OwnPalette; no built-in does. Layouts finds a layout by name: the built-in catalogue unless the template
// adds its own. FitWidths is the glyph widths of its fonts, which the fit rules wrap text with.
public sealed record ResolvedTemplate(Template Template, string Ref, int? Version, bool BuiltIn)
{
    public TemplateLook Look { get; init; } = TemplateLook.None;

    public Palette? OwnPalette { get; init; }

    public int? LatestVersion { get; init; }

    public Guid? OwnerId { get; init; }

    public string? OwnerName { get; init; }

    public bool Archived { get; init; }

    public Func<string, LayoutDefinition?> Layouts { get; init; } = LayoutCatalogue.Find;

    public LayoutDefinition? FindLayout(string name) => Layouts(name);

    public FitWidths FitWidths { get; init; } = FitWidths.Default;
}
