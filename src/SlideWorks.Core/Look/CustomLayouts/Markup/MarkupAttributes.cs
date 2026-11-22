namespace SlideWorks.Core.Look.CustomLayouts.Markup;

// The attributes that carry a binding. Whether one may appear at all is the allow-list's say; these
// are only the meaning of the ones that do.
internal static class MarkupAttributes
{
    public const string CLASS = "class";
    public const string SLOT = "data-slot";
    public const string EACH = "data-each";
    public const string IF = "data-if";
    public const string VAR = "data-var";
    public const string VARIANT = "data-variant";
    public const string STEP = "data-step";

    // The word data-step takes on a data-each element.
    public const string STEP_EACH = "each";

    // data-each may nest this deep.
    public const int MAX_EACH_DEPTH = 2;
    public const int MAX_VARS = 4;
    public const int MAX_VARIANTS = 2;

    // Stop reporting after this many errors.
    public const int MAX_ERRORS = 20;
}
