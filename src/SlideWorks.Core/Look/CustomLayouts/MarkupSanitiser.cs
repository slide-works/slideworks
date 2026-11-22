using SlideWorks.Core.Look.CustomLayouts.Markup;

namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 1 of the custom layout pipeline, and the security boundary for uploaded markup: the markup
// becomes a checked tree or is refused, never cleaned. What passes is read from the allow-list; the
// markup itself is never returned or stored, and no error holds a piece of it. It does not throw for input.
public sealed class MarkupSanitiser(CustomLayoutAllow allow) : IMarkupSanitiser
{
    private readonly MarkupTreeReader reader = new(allow);

    public MarkupSanitiser()
        : this(CustomLayoutAllow.Default)
    {
    }

    public (LayoutTree? Tree, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string markup) => reader.Read(markup);
}
