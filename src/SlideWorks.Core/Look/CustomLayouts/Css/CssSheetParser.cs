using System.Text;

namespace SlideWorks.Core.Look.CustomLayouts.Css;

// The rules of a stylesheet: selector, block, declarations. No at-rule, no nesting. Output is written
// only from what was parsed: each rule as the selector, " {", one "  property: value;" line per
// declaration and "}", each ending in a newline.
internal sealed class CssSheetParser(CssRun run, string classPrefix)
{
    private readonly CssValueParser values = new(run);
    private readonly StringBuilder output = new();
    private int pos;

    private string S => run.Text;

    public string? Run()
    {
        try
        {
            Sheet();
        }
        catch (CssAbortException)
        {
            // The error is already recorded.
        }

        return run.Errors.Count == 0 ? output.ToString() : null;
    }

    private void Sheet()
    {
        var rules = 0;
        while (true)
        {
            SkipSpace();
            if (pos >= S.Length)
            {
                return;
            }

            var c = S[pos];
            if (c == '@')
            {
                run.Fail(pos, CustomLayoutErrorCodes.CSS_AT_RULE);
                SkipAtRule();
                continue;
            }

            if (c is '}' or ';')
            {
                run.Fail(pos, CustomLayoutErrorCodes.CSS_MALFORMED);
                pos++;
                continue;
            }

            if (++rules > CustomLayoutLimits.MAX_CSS_RULES)
            {
                run.Fail(pos, CustomLayoutErrorCodes.CSS_TOO_MANY_RULES);
                return;
            }

            Rule();
        }
    }

    private void Rule()
    {
        var start = pos;
        var depth = 0;
        while (pos < S.Length && S[pos] is not ('{' or '}' or ';'))
        {
            if (S[pos] == '(' && ++depth > CustomLayoutLimits.MAX_PARENTHESIS_DEPTH)
            {
                run.Fail(pos, CustomLayoutErrorCodes.CSS_FUNCTION);
                throw new CssAbortException();
            }

            if (S[pos] == ')')
            {
                depth--;
            }

            pos++;
        }

        if (pos >= S.Length || S[pos] != '{')
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_MALFORMED);
            if (pos < S.Length)
            {
                pos++;
            }

            return;
        }

        var prelude = S[start..pos];
        if (prelude.Count(ch => ch == '[') != prelude.Count(ch => ch == ']'))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_MALFORMED);
            SkipBlock();
            return;
        }

        var selector = CssSelectorParser.Parse(prelude.Trim(), classPrefix);
        if (selector is null)
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_SELECTOR);
            SkipBlock();
            return;
        }

        pos++;
        Block(start, selector);
    }

    private void Block(int ruleStart, string selector)
    {
        var declarations = new StringBuilder();
        var count = 0;
        while (true)
        {
            SkipSpace();
            if (pos >= S.Length)
            {
                run.Fail(ruleStart, CustomLayoutErrorCodes.CSS_MALFORMED);
                return;
            }

            if (S[pos] == '}')
            {
                pos++;
                output.Append(selector).Append(" {\n").Append(declarations).Append("}\n");
                return;
            }

            if (S[pos] == ';')
            {
                pos++;
                continue;
            }

            if (++count == CustomLayoutLimits.MAX_DECLARATIONS_PER_RULE + 1)
            {
                run.Fail(pos, CustomLayoutErrorCodes.CSS_TOO_MANY_RULES);
            }

            var written = Declaration();
            if (written is null)
            {
                while (pos < S.Length && S[pos] is not (';' or '}'))
                {
                    pos++;
                }
            }
            else
            {
                declarations.Append("  ").Append(written).Append(";\n");
            }
        }
    }

    // property: value, from the parsed form. Null when refused (the error is recorded).
    private string? Declaration()
    {
        var start = pos;
        while (pos < S.Length && (char.IsAsciiLetterOrDigit(S[pos]) || S[pos] is '-' or '_'))
        {
            pos++;
        }

        var name = S[start..pos];
        SkipSpace();
        if (name.Length == 0 || pos >= S.Length || S[pos] != ':')
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_MALFORMED);
            return null;
        }

        if (!run.Properties.Contains(name))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_PROPERTY);
            return null;
        }

        pos++;
        values.Pos = pos;
        var value = values.ParseValue(name);
        pos = values.Pos;
        return value is null ? null : name + ": " + value;
    }

    private void SkipSpace()
    {
        while (pos < S.Length && CssRun.IsSpace(S[pos]))
        {
            pos++;
        }
    }

    // From the { at pos to after its matching }.
    private void SkipBlock()
    {
        var depth = 0;
        while (pos < S.Length)
        {
            var c = S[pos++];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth <= 0)
            {
                return;
            }
        }
    }

    private void SkipAtRule()
    {
        while (pos < S.Length)
        {
            var c = S[pos];
            if (c == ';')
            {
                pos++;
                return;
            }

            if (c == '{')
            {
                SkipBlock();
                return;
            }

            pos++;
        }
    }
}
