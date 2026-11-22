namespace SlideWorks.Core.Look.CustomLayouts.Css;

// The first pass over the upload, before any parsing: characters and character pairs that no
// allowed CSS holds anywhere. A backslash (an escape could spell any name), a quote (no value is a
// string), < (it could close the style element), a control or non-ASCII character, ! and a comment.
internal static class CssLexicalCheck
{
    private const string IMPORTANT = "important";

    public static void Run(CssRun run)
    {
        var text = run.Text;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '/' when i + 1 < text.Length && text[i + 1] == '*':
                    run.Fail(i, CustomLayoutErrorCodes.CSS_COMMENT);
                    break;
                case '!':
                    run.Fail(i, FollowedByImportant(text, i + 1) ? CustomLayoutErrorCodes.CSS_IMPORTANT : CustomLayoutErrorCodes.CSS_MALFORMED);
                    break;
                case '\\' or '"' or '\'' or '`' or '<':
                    run.Fail(i, CustomLayoutErrorCodes.CSS_MALFORMED);
                    break;
                default:
                    if ((c < ' ' && c is not ('\n' or '\r' or '\t')) || c > '~')
                    {
                        run.Fail(i, CustomLayoutErrorCodes.CSS_MALFORMED);
                    }

                    break;
            }
        }
    }

    private static bool FollowedByImportant(string text, int start)
    {
        var i = start;
        while (i < text.Length && CssRun.IsSpace(text[i]))
        {
            i++;
        }

        return string.Compare(text, i, IMPORTANT, 0, IMPORTANT.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
