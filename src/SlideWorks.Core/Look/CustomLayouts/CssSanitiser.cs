using System.Text;
using SlideWorks.Core.Look.CustomLayouts.Css;

namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 3 (T10): an uploaded layout.css is parsed, checked against the allow-lists and written again
// from the parsed form, or refused. The CSS that reaches a page is text written here from checked
// rules, never the upload's. Never throws for author input; throws for a malformed class prefix,
// which the pipeline builds.
public sealed class CssSanitiser(CustomLayoutAllow allow) : ICssSanitiser
{
    public CssSanitiser()
        : this(CustomLayoutAllow.Default)
    {
    }

    public (string? Css, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string css, string classPrefix, IReadOnlyList<string> boundVars)
    {
        ArgumentNullException.ThrowIfNull(css);
        ArgumentNullException.ThrowIfNull(boundVars);
        if (!IsPrefix(classPrefix))
        {
            throw new ArgumentException("A class prefix is cl-<8 hex>-<layout name>__.", nameof(classPrefix));
        }

        if (Encoding.UTF8.GetByteCount(css) > CustomLayoutLimits.MAX_PART_BYTES)
        {
            return (null, [CustomLayoutError.Of(CssRun.FileLine(1), CustomLayoutErrorCodes.CSS_TOO_LARGE)]);
        }

        var run = new CssRun(css, allow, boundVars);
        CssLexicalCheck.Run(run);
        if (run.Errors.Count > 0)
        {
            return (null, Ordered(run));
        }

        var written = new CssSheetParser(run, classPrefix).Run();
        return written is null || run.Errors.Count > 0 ? (null, Ordered(run)) : (written, []);
    }

    private static List<CustomLayoutError> Ordered(CssRun run) => [.. run.Errors.OrderBy(LineOf)];

    private static int LineOf(CustomLayoutError error) =>
        int.Parse(error.Path[(error.Path.IndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsPrefix(string? prefix)
    {
        const string START = "cl-";
        const string END = "__";
        const int SCOPE_END = 3 + LayoutScope.LENGTH;
        return prefix is not null
            && prefix.Length > SCOPE_END + 1 + END.Length
            && prefix.StartsWith(START, StringComparison.Ordinal)
            && prefix.EndsWith(END, StringComparison.Ordinal)
            && LayoutScope.IsValid(prefix[START.Length..SCOPE_END])
            && prefix[SCOPE_END] == '-'
            && LayoutClassPrefix.IsLayoutName(prefix[(SCOPE_END + 1)..^END.Length]);
    }
}
