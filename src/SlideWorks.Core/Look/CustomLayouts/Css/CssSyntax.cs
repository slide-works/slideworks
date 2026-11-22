namespace SlideWorks.Core.Look.CustomLayouts.Css;

// The few names the rules of the CSS sanitiser speak about (rule 6 to 10 of T10). They only narrow
// what the allow-list lets through: a name that is not on CustomLayoutAllow is refused whatever this
// says, and no name here is ever allowed by being here.
internal static class CssSyntax
{
    public const string VAR = "var";
    public const string COLOR_MIX = "color-mix";
    public const string REPEAT = "repeat";
    public const string MINMAX = "minmax";
    public const string ROTATE = "rotate";
    public const string TRANSLATE = "translate";

    // Functions whose arguments are arithmetic, so + - * / may stand between terms.
    public static readonly string[] MathFunctions = ["calc", "min", "max", "clamp"];

    // Functions a translate argument may be.
    public static readonly string[] TranslateFunctions = ["calc", "min", "max", "clamp", "var"];

    public static readonly string[] GridTemplateProperties = ["grid-template-columns", "grid-template-rows"];
    public const string FONT_PROPERTY = "font-family";
    public const string POSITION_PROPERTY = "position";
    public const string Z_INDEX_PROPERTY = "z-index";

    public static readonly string[] Positions = ["static", "relative", "absolute"];

    public const string ANGLE_UNIT = "deg";
    public const string PERCENT_UNIT = "%";
    public static readonly string[] LengthUnits = ["px", "em", "%"];

    // color-mix(in srgb, <token> <percentage>, <token>)
    public const string MIX_IN = "in";
    public const string MIX_SPACE = "srgb";

    // Selector pseudo-classes.
    public const string FIRST_CHILD = "first-child";
    public const string LAST_CHILD = "last-child";
    public const string NTH_CHILD = "nth-child";
    public const string ODD = "odd";
    public const string EVEN = "even";
}
