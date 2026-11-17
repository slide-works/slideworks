namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 3: checks an uploaded layout.css and writes it again with every class as <classPrefix><class>
// (classPrefix is a LayoutClassPrefix's Value, the same prefix the pipeline writes on the tree's classes).
// boundVars are the CSS variables the markup binds (such as --v), which the CSS may read beside the
// tokens. Css is null exactly when there are errors.
public interface ICssSanitiser
{
    (string? Css, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string css, string classPrefix, IReadOnlyList<string> boundVars);
}
