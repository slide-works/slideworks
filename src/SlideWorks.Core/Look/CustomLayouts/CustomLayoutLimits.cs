namespace SlideWorks.Core.Look.CustomLayouts;

// The size limits of a custom layout (T10). There is no version number here: the compiled form has its own.
public static class CustomLayoutLimits
{
    // The upload document, and each of its three parts: the markup, the CSS and the slots.
    public const int MAX_UPLOAD_BYTES = 800_000;
    public const int MAX_PART_BYTES = 262_144;

    // Markup: how deep elements nest, the nodes as written, the nodes once every list is drawn, and
    // the classes on one element.
    public const int MAX_MARKUP_DEPTH = 12;
    public const int MAX_NODES = 400;
    public const int MAX_DRAWN_NODES = 2_000;
    public const int MAX_CLASSES_PER_ELEMENT = 8;

    // CSS: rules, declarations in one rule, nested parentheses, comma-separated layers in one value
    // and arguments to one function.
    public const int MAX_CSS_RULES = 300;
    public const int MAX_DECLARATIONS_PER_RULE = 40;
    public const int MAX_PARENTHESIS_DEPTH = 6;
    public const int MAX_VALUE_LAYERS = 8;
    public const int MAX_FUNCTION_ARGUMENTS = 16;

    // Slots, counting the fields of a list slot.
    public const int MAX_SLOTS = 24;
}
