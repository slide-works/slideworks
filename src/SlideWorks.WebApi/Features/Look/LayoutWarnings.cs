using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look;

// D9: a slide whose layout is outside the deck's template is kept and still renders, with a warning. set_look warns about
// every such slide, and add_slide and update_slide about the slide they wrote, all in this one wording. A custom layout of
// the deck's own template is never outside it (K18).
public static class LayoutWarnings
{
    public static IReadOnlyList<string> For(ResolvedTemplate template, int number, string layout)
        => template.AllowsLayout(layout) ? [] : [$"Slide {number}: layout '{layout}' is not in the '{template.Template.Name}' template"];
}
