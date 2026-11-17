namespace SlideWorks.Core.Look.CustomLayouts;

// The one place a custom layout's class prefix is made: cl-<scope>-<layout name>__. The server writes
// it on every class, in the compiled CSS and in the compiled tree alike, so two templates' layouts of
// one name never style each other and no layout styles the app. The client draws the class names it
// is given and never builds one. A prefix exists only for a checked scope and a checked layout name,
// and it is only ever put before a checked class name: anything else throws, because an earlier
// stage should have refused it.
public sealed class LayoutClassPrefix
{
    private LayoutClassPrefix(string value) => Value = value;

    // The prefix as text, as the CSS stage is given it.
    public string Value { get; }

    // scope is 8 lower-case hex characters. layoutName is lower-case letters, digits and single
    // hyphens, starting with a letter and not ending with a hyphen (bar-chart; LayoutScope.UNNAMED).
    public static LayoutClassPrefix Of(string scope, string layoutName)
    {
        if (!LayoutScope.IsValid(scope))
        {
            throw new ArgumentException("A layout's scope is 8 lower-case hex characters.", nameof(scope));
        }

        if (!IsLayoutName(layoutName))
        {
            throw new ArgumentException("A layout's name is lower-case letters, digits and single hyphens, starting with a letter.", nameof(layoutName));
        }

        return new LayoutClassPrefix($"cl-{scope}-{layoutName}__");
    }

    // The scoped form of one class of the author's markup or CSS. The author's class is never
    // trusted as already scoped: a class that starts with cl- gets the prefix like any other.
    public string Apply(string className)
    {
        if (!IsClassName(className))
        {
            throw new ArgumentException("A class name is lower-case letters, digits and hyphens.", nameof(className));
        }

        return Value + className;
    }

    // Whether a class of a stored tree is this layout's: the prefix, then a class name. A class with
    // another layout's prefix, or with none, is not.
    public bool Owns(string? className) =>
        className is not null
        && className.StartsWith(Value, StringComparison.Ordinal)
        && IsClassName(className[Value.Length..]);

    public override string ToString() => Value;

    private static bool IsLayoutName(string? name) =>
        IsClassName(name)
        && name![0] is >= 'a' and <= 'z'
        && name[^1] != '-'
        && !name.Contains("--", StringComparison.Ordinal);

    private static bool IsClassName(string? name) =>
        !string.IsNullOrEmpty(name) && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
}
