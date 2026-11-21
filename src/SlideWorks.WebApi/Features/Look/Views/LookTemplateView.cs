using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// The template half of a LookView (L6). Chrome is the effective chrome: the template's defaults after the deck's
// overrides. Shape's shadow is the card lift on the deck's paper (CardLift): the template's own, or the dark lift on a
// dark palette.
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
    public static LookTemplateView From(Template template, ChromeOverridesView overrides, string? paper)
        => new(
            template.Name,
            template.Label,
            template.Layouts,
            overrides.ApplyTo(template.Chrome),
            template.Type,
            template.Shape with { Shadow = CardLift.For(template.Shape.Shadow, paper) },
            template.Ornament,
            template.TitleArt);
}
