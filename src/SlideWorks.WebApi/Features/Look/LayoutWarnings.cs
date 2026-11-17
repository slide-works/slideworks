using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look;

// D9: a slide whose layout is outside the deck's template is kept and still renders, with a warning. set_look warns about
// every such slide, and add_slide and update_slide about the slide they wrote, all in this one wording.
public static class LayoutWarnings
{
    public static IReadOnlyList<string> For(Template template, int number, string layout)
        => template.Allows(layout) ? [] : [$"Slide {number}: layout '{layout}' is not in the '{template.Name}' template"];
}
