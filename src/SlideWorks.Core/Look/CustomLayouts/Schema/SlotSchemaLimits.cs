namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// The limits of a slots.json that CustomLayoutLimits does not hold (the slot count is MAX_SLOTS there).
public static class SlotSchemaLimits
{
    public const int MAX_TEXT = 500;
    public const int MIN_LIST_ITEMS = 1;
    public const int MAX_LIST_ITEMS = 12;
    public const int MAX_ENUM_VALUES = 8;

    // A layout's name, in characters.
    public const int MIN_LAYOUT_NAME = 2;
    public const int MAX_LAYOUT_NAME = 31;

    // A slot name or an enum value, so that it can stand in an error message (CustomLayoutError.MAX_IDENTIFIER).
    public const int MAX_NAME = 64;

    public const int MAX_DESCRIPTION = 200;

    // A number's bounds: whole numbers' range, so a bound fits the schema's integers and a CSS value stays small.
    public const double MAX_NUMBER_BOUND = 1_000_000_000;

    // Lists nest twice: a list, and a list among its fields.
    public const int MAX_LIST_LEVELS = 2;

    public const int MAX_ERRORS = 20;
}
