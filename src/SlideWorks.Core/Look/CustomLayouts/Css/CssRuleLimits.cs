namespace SlideWorks.Core.Look.CustomLayouts.Css;

// The limits of the CSS rules that are not size limits of the whole upload (those are in
// CustomLayoutLimits). Each is one refusal of the CSS sanitiser.
public static class CssRuleLimits
{
    // Selectors: how many in a list, and how many compounds in one selector.
    public const int MAX_SELECTORS = 8;
    public const int MAX_COMPOUNDS = 4;

    // :nth-child(<n>) takes an integer from 1 to this.
    public const int MAX_NTH_CHILD = 20;

    // A number: digits before and after the point.
    public const int MAX_INTEGER_DIGITS = 6;
    public const int MAX_FRACTION_DIGITS = 3;

    // repeat(<n>, ...) takes an integer from 1 to this.
    public const int MAX_REPEAT = 12;

    // z-index is an integer from 0 to this.
    public const int MAX_Z_INDEX = 10;
}
