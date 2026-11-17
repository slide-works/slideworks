using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// The template half of a LookView (L6). Chrome is the effective chrome: the template's defaults after the deck's
// overrides.
public sealed record LookTemplateView(
    string Name,
    string Label,
    IReadOnlyList<string> Layouts,
    TemplateChrome Chrome,
    TemplateType Type,
    TemplateShape Shape,
    string Ornament,
    string? TitleArt)
{
    public static LookTemplateView From(Template template, ChromeOverridesView overrides)
        => new(
            template.Name,
            template.Label,
            template.Layouts,
            overrides.ApplyTo(template.Chrome),
            template.Type,
            template.Shape,
            template.Ornament,
            template.TitleArt);
}
