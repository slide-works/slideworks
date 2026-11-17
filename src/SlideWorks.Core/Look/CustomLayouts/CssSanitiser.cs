namespace SlideWorks.Core.Look.CustomLayouts;

public sealed class CssSanitiser : ICssSanitiser
{
    // TODO: refuses every upload until the CSS sanitiser is written; the real body checks the CSS
    // against the allow-list and writes it again with every class prefixed.
    public (string? Css, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string css, string classPrefix, IReadOnlyList<string> boundVars) =>
        (null, [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE)]);
}
