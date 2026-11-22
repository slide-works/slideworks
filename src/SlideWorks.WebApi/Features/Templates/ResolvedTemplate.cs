using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Layouts;

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

    // The approved custom layouts of this template version, in the manifest's order; empty on a built-in. Unapproved ones
    // are never here: this is the second lock behind the publish gate.
    public IReadOnlyList<TemplateLayout> CustomLayouts { get; init; } = [];

    // True when the layout is one the deck's template allows: a built-in it names, a live-room layout, or a custom layout of
    // this version. A custom layout of the deck's own template never warns (K18).
    public bool AllowsLayout(string name) => Template.Allows(name) || CustomLayouts.Any(c => string.Equals(c.Layout.Name, name, StringComparison.Ordinal));

    // True when the name is a custom layout of this version and not a built-in (a built-in name always wins, T4, when the catalogue has one).
    public bool IsCustomLayout(string name)
        => LayoutCatalogue.Find(name) is null && CustomLayouts.Any(c => string.Equals(c.Layout.Name, name, StringComparison.Ordinal));

    public FitWidths FitWidths { get; init; } = FitWidths.Default;
}
