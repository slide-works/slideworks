namespace SlideWorks.Core.Look.CustomLayouts.Markup;

// Reads the value of one binding attribute, after entities are decoded. Each method returns null (or
// false) for any value that is not exactly the form, so the caller refuses it. Nothing is trimmed
// beyond the single space the form allows, and no pattern engine is used: each check is a loop over characters.
internal static class BindingParser
{
    private const string SW_PREFIX = "--sw-";

    private static bool IsLower(char c) => c is >= 'a' and <= 'z';

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    // [a-z][a-z0-9-]*
    public static bool IsClass(string text) =>
        text.Length > 0 && IsLower(text[0]) && text.All(c => IsLower(c) || IsDigit(c) || c == '-');

    // [a-z][a-z0-9_]*
    public static bool IsSlotName(string text) =>
        text.Length > 0 && IsLower(text[0]) && text.All(c => IsLower(c) || IsDigit(c) || c == '_');

    // --[a-z][a-z0-9-]*, and never --sw-…: a layout may not re-point a token.
    public static bool IsVariable(string text) =>
        text.Length > 2 && text.StartsWith("--", StringComparison.Ordinal) && IsLower(text[2])
        && text.Skip(2).All(c => IsLower(c) || IsDigit(c) || c == '-')
        && !text.StartsWith(SW_PREFIX, StringComparison.Ordinal);

    // The names of a class attribute, split on white space. False when any is not a class.
    public static bool TryClasses(string value, out IReadOnlyList<string> classes)
    {
        var names = value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        classes = names;
        return names.All(IsClass);
    }

    // "--p: progress; --q: other" as (name, slot) pairs. False for anything else.
    public static bool TryVars(string value, out IReadOnlyList<TreeVar> vars)
    {
        var found = new List<TreeVar>();
        vars = found;
        var parts = value.Split(';');
        if (parts.Length > MarkupAttributes.MAX_VARS)
        {
            return false;
        }

        foreach (var part in parts)
        {
            var pair = part.Split(':');
            if (pair.Length != 2)
            {
                return false;
            }

            var name = pair[0].Trim(' ');
            var slot = pair[1].Trim(' ');
            if (!IsVariable(name) || !IsSlotName(slot) || found.Any(v => v.Name == name))
            {
                return false;
            }

            found.Add(new TreeVar(name, slot));
        }

        return true;
    }

    // One or two slot names split by a single space.
    public static bool TryVariants(string value, out IReadOnlyList<TreeVariant> variants)
    {
        var names = value.Split(' ');
        variants = [.. names.Select(TreeVariant.Bare)];
        return names.Length is >= 1 and <= MarkupAttributes.MAX_VARIANTS && names.All(IsSlotName) && names.Distinct().Count() == names.Length;
    }

    // An integer 1 to MAX_STEPS written without a sign or a leading zero, or null.
    public static int? Step(string value)
    {
        if (value.Length is < 1 or > 2 || !value.All(IsDigit) || value[0] == '0')
        {
            return null;
        }

        var step = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return step <= CustomLayoutLimits.MAX_STEPS ? step : null;
    }

    // A tag or attribute name that may be named in a message: the error type takes only these.
    public static bool IsNameable(string name) =>
        name.Length is > 0 and <= CustomLayoutError.MAX_IDENTIFIER && name.All(c => IsLower(c) || IsDigit(c) || c is '-' or '_');
}
