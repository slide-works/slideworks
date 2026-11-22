namespace SlideWorks.Core.Look.CustomLayouts.Css;

// A selector list: up to 8 selectors, each up to 4 compounds joined by a space or >. A compound is
// one or more .class, then at most one of :first-child, :last-child, :nth-child(n | odd | even).
// Nothing else parses. Written again with every class prefixed.
internal static class CssSelectorParser
{
    // The selector list as text, or null when any part of it is not allowed.
    public static string? Parse(string prelude, string classPrefix)
    {
        var selectors = prelude.Split(',');
        if (selectors.Length > CssRuleLimits.MAX_SELECTORS)
        {
            return null;
        }

        var written = new List<string>();
        foreach (var selector in selectors)
        {
            var one = ParseOne(selector.Trim(), classPrefix);
            if (one is null)
            {
                return null;
            }

            written.Add(one);
        }

        return CssRun.Join(written, ", ");
    }

    private static string? ParseOne(string s, string classPrefix)
    {
        var output = new System.Text.StringBuilder();
        var i = 0;
        var compounds = 0;
        while (true)
        {
            if (++compounds > CssRuleLimits.MAX_COMPOUNDS || !ParseCompound(s, ref i, classPrefix, output))
            {
                return null;
            }

            var spaced = false;
            while (i < s.Length && CssRun.IsSpace(s[i]))
            {
                i++;
                spaced = true;
            }

            if (i >= s.Length)
            {
                return output.ToString();
            }

            if (s[i] == '>')
            {
                i++;
                while (i < s.Length && CssRun.IsSpace(s[i]))
                {
                    i++;
                }

                output.Append(" > ");
            }
            else if (spaced)
            {
                output.Append(' ');
            }
            else
            {
                return null;
            }
        }
    }

    private static bool ParseCompound(string s, ref int i, string classPrefix, System.Text.StringBuilder output)
    {
        var classes = 0;
        while (i < s.Length && s[i] == '.')
        {
            i++;
            var start = i;
            while (i < s.Length && (s[i] is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                i++;
            }

            if (i == start || s[start] is not (>= 'a' and <= 'z'))
            {
                return false;
            }

            output.Append('.').Append(classPrefix).Append(s, start, i - start);
            classes++;
        }

        if (classes == 0)
        {
            return false;
        }

        if (i < s.Length && s[i] == ':')
        {
            i++;
            var start = i;
            while (i < s.Length && (s[i] is (>= 'a' and <= 'z') or '-'))
            {
                i++;
            }

            var name = s[start..i];
            if (name is CssSyntax.FIRST_CHILD or CssSyntax.LAST_CHILD)
            {
                output.Append(':').Append(name);
                return true;
            }

            if (name != CssSyntax.NTH_CHILD || i >= s.Length || s[i] != '(')
            {
                return false;
            }

            var close = s.IndexOf(')', i);
            if (close < 0)
            {
                return false;
            }

            var argument = s[(i + 1)..close];
            i = close + 1;
            if (argument is CssSyntax.ODD or CssSyntax.EVEN || IsNthIndex(argument))
            {
                output.Append(':').Append(CssSyntax.NTH_CHILD).Append('(').Append(argument).Append(')');
                return true;
            }

            return false;
        }

        return true;
    }

    private static bool IsNthIndex(string argument) =>
        argument.Length is 1 or 2
        && argument.All(char.IsAsciiDigit)
        && argument[0] != '0'
        && int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture) <= CssRuleLimits.MAX_NTH_CHILD;
}
