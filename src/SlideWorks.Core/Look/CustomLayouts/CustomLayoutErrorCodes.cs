namespace SlideWorks.Core.Look.CustomLayouts;

// The error codes of custom layouts (T10) and the plain sentence of each. This class is the runtime
// source; a test keeps it equal to src/contracts/custom-layout-errors.json. A code is its family's
// prefix plus a word: markup_, css_ and slots_ for the three uploaded parts, then the cross-check's
// and the codes of a whole layout.
public static class CustomLayoutErrorCodes
{
    public const string MARKUP_TOO_LARGE = "markup_too_large";

    public const string MARKUP_MALFORMED = "markup_malformed";

    public const string MARKUP_TAG = "markup_tag";

    public const string MARKUP_ATTRIBUTE = "markup_attribute";

    public const string MARKUP_TEXT = "markup_text";

    public const string MARKUP_COMMENT = "markup_comment";

    public const string MARKUP_CLASS = "markup_class";

    public const string MARKUP_BINDING = "markup_binding";

    public const string MARKUP_DEPTH = "markup_depth";

    public const string MARKUP_TOO_MANY_NODES = "markup_too_many_nodes";

    public const string CSS_TOO_LARGE = "css_too_large";

    public const string CSS_MALFORMED = "css_malformed";

    public const string CSS_COMMENT = "css_comment";

    public const string CSS_AT_RULE = "css_at_rule";

    public const string CSS_SELECTOR = "css_selector";

    public const string CSS_PROPERTY = "css_property";

    public const string CSS_VALUE = "css_value";

    public const string CSS_FUNCTION = "css_function";

    public const string CSS_COLOUR = "css_colour";

    public const string CSS_FONT = "css_font";

    public const string CSS_VARIABLE = "css_variable";

    public const string CSS_IMPORTANT = "css_important";

    public const string CSS_TOO_MANY_RULES = "css_too_many_rules";

    public const string SLOTS_TOO_LARGE = "slots_too_large";

    public const string SLOTS_MALFORMED = "slots_malformed";

    public const string SLOTS_NAME = "slots_name";

    public const string SLOTS_DESCRIPTION = "slots_description";

    public const string SLOTS_SURFACE = "slots_surface";

    public const string SLOTS_KEY = "slots_key";

    public const string SLOTS_TYPE = "slots_type";

    public const string SLOTS_LIMIT = "slots_limit";

    public const string SLOTS_STEPS = "slots_steps";

    public const string SLOTS_DEPTH = "slots_depth";

    public const string SLOTS_TOO_MANY = "slots_too_many";

    public const string BINDING_UNKNOWN_SLOT = "binding_unknown_slot";

    public const string BINDING_WRONG_KIND = "binding_wrong_kind";

    public const string SLOT_UNUSED = "slot_unused";

    public const string STEPS_MISMATCH = "steps_mismatch";

    public const string CUSTOM_LAYOUTS_UNAVAILABLE = "custom_layouts_unavailable";

    public const string LAYOUT_REFUSED = "layout_refused";

    public const string LAYOUT_NOT_FOUND = "layout_not_found";

    public const string LAYOUT_CHANGED = "layout_changed";

    public const string LAYOUT_OVERFLOWS = "layout_overflows";

    public const string LAYOUT_LOW_CONTRAST = "layout_low_contrast";

    private static readonly IReadOnlyDictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MARKUP_TOO_LARGE] = "The markup is too large.",
        [MARKUP_MALFORMED] = "The markup can't be read. Check that every tag is closed and every attribute value is in double quotes.",
        [MARKUP_TAG] = "This tag is not allowed in a layout.",
        [MARKUP_ATTRIBUTE] = "This attribute is not allowed here.",
        [MARKUP_TEXT] = "Text can't be written in the markup. Put it in a slot.",
        [MARKUP_COMMENT] = "Comments are not allowed in the markup.",
        [MARKUP_CLASS] = "This class name is not allowed. Use lower-case letters, digits and hyphens.",
        [MARKUP_BINDING] = "This binding can't be read.",
        [MARKUP_DEPTH] = "The markup is nested too deeply.",
        [MARKUP_TOO_MANY_NODES] = "The markup has too many elements.",
        [CSS_TOO_LARGE] = "The CSS is too large.",
        [CSS_MALFORMED] = "The CSS can't be read.",
        [CSS_COMMENT] = "Comments are not allowed in the CSS.",
        [CSS_AT_RULE] = "This at-rule is not allowed.",
        [CSS_SELECTOR] = "This selector is not allowed. Use class selectors only.",
        [CSS_PROPERTY] = "This property is not allowed.",
        [CSS_VALUE] = "This value is not allowed for this property.",
        [CSS_FUNCTION] = "This function is not allowed.",
        [CSS_COLOUR] = "Colours must come from the template's tokens.",
        [CSS_FONT] = "Fonts must come from the template's tokens.",
        [CSS_VARIABLE] = "This variable is neither a token nor a variable the markup binds.",
        [CSS_IMPORTANT] = "!important is not allowed.",
        [CSS_TOO_MANY_RULES] = "The CSS has too many rules.",
        [SLOTS_TOO_LARGE] = "The slots file is too large.",
        [SLOTS_MALFORMED] = "The slots file can't be read. It must be one JSON object.",
        [SLOTS_NAME] = "This name is not allowed. Layout names use lower-case letters, digits and single hyphens; slot names use lower-case letters, digits and underscores.",
        [SLOTS_DESCRIPTION] = "The layout needs a one-line description.",
        [SLOTS_SURFACE] = "The surface must be light, dark or either.",
        [SLOTS_KEY] = "This key is not known here.",
        [SLOTS_TYPE] = "This slot type is not known or not allowed here.",
        [SLOTS_LIMIT] = "This limit is missing or out of range.",
        [SLOTS_STEPS] = "Steps must name a top-level list slot.",
        [SLOTS_DEPTH] = "Lists are nested too deeply.",
        [SLOTS_TOO_MANY] = "The layout has too many slots.",
        [BINDING_UNKNOWN_SLOT] = "This binding names a slot the layout does not have.",
        [BINDING_WRONG_KIND] = "This binding needs a slot of another type.",
        [SLOT_UNUSED] = "This slot is not used in the markup.",
        [STEPS_MISMATCH] = "The steps in the markup do not match the steps in the slots file.",
        [CUSTOM_LAYOUTS_UNAVAILABLE] = "Custom layouts are not available yet.",
        [LAYOUT_REFUSED] = "this layout was refused; see errors",
        [LAYOUT_NOT_FOUND] = "This layout was not found.",
        [LAYOUT_CHANGED] = "This layout has changed since it was reviewed. Review it again.",
        [LAYOUT_OVERFLOWS] = "This layout's content does not fit inside the slide.",
        [LAYOUT_LOW_CONTRAST] = "This layout's text does not have enough contrast with what is behind it.",
    };

    // Every code, in the contract file's order.
    public static IReadOnlyList<string> All { get; } =
    [
        MARKUP_TOO_LARGE,
        MARKUP_MALFORMED,
        MARKUP_TAG,
        MARKUP_ATTRIBUTE,
        MARKUP_TEXT,
        MARKUP_COMMENT,
        MARKUP_CLASS,
        MARKUP_BINDING,
        MARKUP_DEPTH,
        MARKUP_TOO_MANY_NODES,
        CSS_TOO_LARGE,
        CSS_MALFORMED,
        CSS_COMMENT,
        CSS_AT_RULE,
        CSS_SELECTOR,
        CSS_PROPERTY,
        CSS_VALUE,
        CSS_FUNCTION,
        CSS_COLOUR,
        CSS_FONT,
        CSS_VARIABLE,
        CSS_IMPORTANT,
        CSS_TOO_MANY_RULES,
        SLOTS_TOO_LARGE,
        SLOTS_MALFORMED,
        SLOTS_NAME,
        SLOTS_DESCRIPTION,
        SLOTS_SURFACE,
        SLOTS_KEY,
        SLOTS_TYPE,
        SLOTS_LIMIT,
        SLOTS_STEPS,
        SLOTS_DEPTH,
        SLOTS_TOO_MANY,
        BINDING_UNKNOWN_SLOT,
        BINDING_WRONG_KIND,
        SLOT_UNUSED,
        STEPS_MISMATCH,
        CUSTOM_LAYOUTS_UNAVAILABLE,
        LAYOUT_REFUSED,
        LAYOUT_NOT_FOUND,
        LAYOUT_CHANGED,
        LAYOUT_OVERFLOWS,
        LAYOUT_LOW_CONTRAST,
    ];

    // The code's sentence. An unknown code throws.
    public static string Text(string code) =>
        Texts.TryGetValue(code, out var text) ? text : throw new ArgumentException($"'{code}' is not a custom layout error code.", nameof(code));
}
