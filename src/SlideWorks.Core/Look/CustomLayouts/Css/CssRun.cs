using System.Text;

namespace SlideWorks.Core.Look.CustomLayouts.Css;

// Thrown inside the sanitiser to stop at once (parentheses too deep, too many rules). Always caught
// by CssSheetParser; it never leaves Core.
public sealed class CssAbortException : Exception
{
}

// One run over one upload: the text, where each character's line is, the allow-lists as sets, the
// bound variables and the errors found so far.
internal sealed class CssRun
{
    private const string PATH = "layout.css";

    private readonly int[] lines;
    private readonly HashSet<string> seen = [];

    public CssRun(string text, CustomLayoutAllow allow, IReadOnlyList<string> boundVars)
    {
        Text = text;
        lines = new int[text.Length + 1];
        var line = 1;
        for (var i = 0; i < text.Length; i++)
        {
            lines[i] = line;
            if (text[i] == '\n')
            {
                line++;
            }
        }

        lines[text.Length] = line;
        Properties = [.. allow.CssProperties];
        Functions = [.. allow.CssFunctions];
        Units = [.. allow.CssUnits];
        Keywords = [.. allow.CssKeywords];
        Tokens = [.. allow.Tokens];
        FontTokens = [.. allow.FontTokens];
        Bound = [.. boundVars.Where(v => v is not null)];
    }

    public string Text { get; }
    internal HashSet<string> Properties { get; }
    internal HashSet<string> Functions { get; }
    internal HashSet<string> Units { get; }
    internal HashSet<string> Keywords { get; }
    internal HashSet<string> Tokens { get; }
    internal HashSet<string> FontTokens { get; }
    internal HashSet<string> Bound { get; }
    internal List<CustomLayoutError> Errors { get; } = [];

    public int Length => Text.Length;

    public static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r';

    // An error at a position in the text: its line, the code's own text, nothing from the upload.
    public void Fail(int position, string code)
    {
        var line = lines[Math.Clamp(position, 0, Text.Length)];
        if (seen.Add(line + ":" + code))
        {
            Errors.Add(CustomLayoutError.Of($"{PATH}:{line}", code));
        }
    }

    // Forgets the errors recorded after the first count.
    public void Truncate(int count)
    {
        for (var i = Errors.Count - 1; i >= count; i--)
        {
            var path = Errors[i].Path;
            seen.Remove(path[(path.IndexOf(':') + 1)..] + ":" + Errors[i].Code);
            Errors.RemoveAt(i);
        }
    }

    public static string FileLine(int line) => $"{PATH}:{line}";

    public static string Join(IEnumerable<string> parts, string separator)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (builder.Length > 0)
            {
                builder.Append(separator);
            }

            builder.Append(part);
        }

        return builder.ToString();
    }
}
