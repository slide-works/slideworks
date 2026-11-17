namespace SlideWorks.Core.Look.CustomLayouts;

// How a custom layout reveals. Kind is none, list (one step per item of the top-level list named in
// List) or fixed (Count numbered steps). Count is 0 unless the kind is fixed.
public sealed record CustomSteps(string Kind, string? List, int Count)
{
    public const string NONE = "none";

    public const string LIST = "list";

    public const string FIXED = "fixed";
}
