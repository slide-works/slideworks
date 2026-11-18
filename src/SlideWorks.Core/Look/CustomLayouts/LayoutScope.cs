namespace SlideWorks.Core.Look.CustomLayouts;

// The scope of a compiled layout's classes: the first 8 characters of the upload's asset id, as
// lower-case hex. LayoutClassPrefix makes the class prefix from it.
public static class LayoutScope
{
    public const int LENGTH = 8;

    // The class prefix written when the slots file gave no usable name.
    public const string UNNAMED = "x";

    public static bool IsValid(string? scope) =>
        scope is { Length: LENGTH } && scope.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
