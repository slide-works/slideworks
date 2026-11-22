using System.Globalization;

namespace SlideWorks.Core.Look.CustomLayouts.Css;

// Parses one declaration's value into numbers, dimensions, keywords, functions and groups, and writes
// it again from the parsed form. Every error is recorded on the run and returns null; nothing is ever
// passed through as written. Parentheses are counted as the parser descends: the seventh level stops
// the whole run (CssAbortException), so no input makes it recurse deeper than 6.
internal sealed class CssValueParser(CssRun run)
{
    public int Pos { get; set; }

    private string S => run.Text;

    // The value at Pos, up to its ; or }, or null when refused.
    public string? ParseValue(string property)
    {
        var before = run.Errors.Count;
        var value = ParseLayers(property);
        if (value is null && property == CssSyntax.FONT_PROPERTY && run.Errors.Count > before)
        {
            // A font is named or built wrongly: one error says so, not the parts of it.
            run.Truncate(before);
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_FONT);
        }

        return value;
    }

    private string? ParseLayers(string property)
    {
        var layers = new List<string>();
        List<CssTerm>? first = null;
        while (true)
        {
            if (!ParseSequence(false, 0, property, out var terms))
            {
                return null;
            }

            if (terms.Count == 0)
            {
                run.Fail(Pos, CustomLayoutErrorCodes.CSS_VALUE);
                return null;
            }

            first ??= terms;
            layers.Add(CssRun.Join(terms.Select(t => t.Text), " "));
            if (Pos >= S.Length)
            {
                run.Fail(Pos, CustomLayoutErrorCodes.CSS_MALFORMED);
                return null;
            }

            switch (S[Pos])
            {
                case ',':
                    if (layers.Count >= CustomLayoutLimits.MAX_VALUE_LAYERS)
                    {
                        run.Fail(Pos, CustomLayoutErrorCodes.CSS_VALUE);
                        return null;
                    }

                    Pos++;
                    continue;
                case ';' or '}':
                    return CheckProperty(property, layers, first) ? CssRun.Join(layers, ", ") : null;
                default:
                    run.Fail(Pos, CustomLayoutErrorCodes.CSS_VALUE);
                    return null;
            }
        }
    }

    private bool CheckProperty(string property, List<string> layers, List<CssTerm> first)
    {
        var single = layers.Count == 1 && first.Count == 1 ? first[0] : null;
        if (property == CssSyntax.FONT_PROPERTY)
        {
            if (single is { Kind: CssTermKind.Function, Name: CssSyntax.VAR, IsFont: true })
            {
                return true;
            }

            run.Fail(Pos, CustomLayoutErrorCodes.CSS_FONT);
            return false;
        }

        var ok = property switch
        {
            CssSyntax.POSITION_PROPERTY => single is { Kind: CssTermKind.Keyword } && CssSyntax.Positions.Contains(single.Name),
            CssSyntax.Z_INDEX_PROPERTY => single is { Kind: CssTermKind.Integer, Number: >= 0 and <= CssRuleLimits.MAX_Z_INDEX },
            _ => true,
        };
        if (!ok)
        {
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_VALUE);
        }

        return ok;
    }

    // Terms until a , ) ; or } (left unread). Null when one is refused.
    private bool ParseSequence(bool math, int depth, string property, out List<CssTerm> terms)
    {
        terms = [];
        while (true)
        {
            SkipSpace();
            if (Pos >= S.Length)
            {
                run.Fail(Pos, CustomLayoutErrorCodes.CSS_MALFORMED);
                return false;
            }

            if (S[Pos] is ',' or ')' or ';' or '}')
            {
                return true;
            }

            var term = ParseTerm(math, depth, property);
            if (term is null)
            {
                return false;
            }

            terms.Add(term);
        }
    }

    private CssTerm? ParseTerm(bool math, int depth, string property)
    {
        var c = S[Pos];
        var next = Pos + 1 < S.Length ? S[Pos + 1] : '\0';
        if (char.IsAsciiDigit(c) || c == '.' || (c == '-' && (char.IsAsciiDigit(next) || next == '.')))
        {
            return ParseNumber(math);
        }

        if (c == '#')
        {
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_COLOUR);
            return null;
        }

        if (c == '{' || c == '>')
        {
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_MALFORMED);
            return null;
        }

        if (math && c is '*' or '/' or '+' or '-' && CssRun.IsSpace(next))
        {
            Pos++;
            return new CssTerm(CssTermKind.Operator, c.ToString());
        }

        if (math && c is '*' or '/')
        {
            Pos++;
            return new CssTerm(CssTermKind.Operator, c.ToString());
        }

        if (c == '(' && math)
        {
            return ParseGroup(depth);
        }

        if (IsIdentChar(c))
        {
            return ParseWord(math, depth, property);
        }

        // An opening [ with no ] before the end of the declaration is unbalanced, not just unsupported.
        run.Fail(Pos, c == '[' && !HasClosing(']') ? CustomLayoutErrorCodes.CSS_MALFORMED : CustomLayoutErrorCodes.CSS_VALUE);
        return null;
    }

    // Whether the declaration holds the character after Pos, before its ; or }. One scan, and the caller
    // refuses straight after, so parsing stays linear.
    private bool HasClosing(char close)
    {
        for (var i = Pos + 1; i < S.Length && S[i] is not (';' or '}'); i++)
        {
            if (S[i] == close)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';

    private CssTerm? ParseNumber(bool math)
    {
        var start = Pos;
        var i = Pos;
        if (S[i] == '-')
        {
            i++;
        }

        var integerStart = i;
        while (i < S.Length && char.IsAsciiDigit(S[i]))
        {
            i++;
        }

        var integerDigits = i - integerStart;
        var fractionDigits = 0;
        var hasPoint = false;
        if (i < S.Length && S[i] == '.')
        {
            hasPoint = true;
            i++;
            var fractionStart = i;
            while (i < S.Length && char.IsAsciiDigit(S[i]))
            {
                i++;
            }

            fractionDigits = i - fractionStart;
        }

        if (integerDigits + fractionDigits == 0
            || integerDigits > CssRuleLimits.MAX_INTEGER_DIGITS
            || fractionDigits > CssRuleLimits.MAX_FRACTION_DIGITS
            || (hasPoint && fractionDigits == 0))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        var number = S[start..i];
        var value = double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
        var unitStart = i;
        if (i < S.Length && S[i] == '%')
        {
            i++;
        }
        else
        {
            while (i < S.Length && char.IsAsciiLetter(S[i]))
            {
                i++;
            }
        }

        var unit = S[unitStart..i];
        Pos = i;
        if (unit.Length > 0 && !run.Units.Contains(unit))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        if (!AtBoundary(math))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        var kind = hasPoint ? CssTermKind.Number : CssTermKind.Integer;
        if (unit.Length > 0)
        {
            kind = CssTermKind.Dimension;
        }

        return new CssTerm(kind, number + unit, Number: value, Unit: unit);
    }

    // After a term: a space, a separator or the end of the argument.
    private bool AtBoundary(bool math) =>
        Pos >= S.Length || CssRun.IsSpace(S[Pos]) || S[Pos] is ',' or ')' or ';' or '}' || (math && S[Pos] is '*' or '/');

    private CssTerm? ParseGroup(int depth)
    {
        Enter(depth);
        Pos++;
        if (!ParseSequence(true, depth + 1, string.Empty, out var terms))
        {
            return null;
        }

        if (S[Pos] != ')' || terms.Count == 0)
        {
            run.Fail(Pos, S[Pos] is ';' or '}' ? CustomLayoutErrorCodes.CSS_MALFORMED : CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        Pos++;
        return AtBoundary(true)
            ? new CssTerm(CssTermKind.Group, "(" + CssRun.Join(terms.Select(t => t.Text), " ") + ")")
            : Refuse(CustomLayoutErrorCodes.CSS_VALUE);
    }

    private CssTerm? Refuse(string code)
    {
        run.Fail(Pos, code);
        return null;
    }

    private void Enter(int depth)
    {
        if (depth + 1 > CustomLayoutLimits.MAX_PARENTHESIS_DEPTH)
        {
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_FUNCTION);
            throw new CssAbortException();
        }
    }

    private CssTerm? ParseWord(bool math, int depth, string property)
    {
        var start = Pos;
        while (Pos < S.Length && IsIdentChar(S[Pos]))
        {
            Pos++;
        }

        var word = S[start..Pos];
        if (Pos < S.Length && S[Pos] == '(')
        {
            if (!run.Functions.Contains(word))
            {
                run.Fail(start, CustomLayoutErrorCodes.CSS_FUNCTION);
                return null;
            }

            Enter(depth);
            Pos++;
            var function = ParseFunction(word, depth + 1, property);
            if (function is not null && !AtBoundary(math))
            {
                return Refuse(CustomLayoutErrorCodes.CSS_VALUE);
            }

            return function;
        }

        if (!run.Keywords.Contains(word) || !AtBoundary(math))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        return new CssTerm(CssTermKind.Keyword, word, word);
    }

    // Pos is just after the (; leaves Pos after the ).
    private CssTerm? ParseFunction(string name, int depth, string property)
    {
        if (name == CssSyntax.VAR)
        {
            return ParseVar(property);
        }

        var gridOnly = name is CssSyntax.REPEAT or CssSyntax.MINMAX;
        if (gridOnly && !CssSyntax.GridTemplateProperties.Contains(property))
        {
            run.Fail(Pos, CustomLayoutErrorCodes.CSS_FUNCTION);
            return null;
        }

        var math = CssSyntax.MathFunctions.Contains(name);
        if (!ParseArguments(math, depth, property, out var arguments))
        {
            return null;
        }

        var valid = name switch
        {
            CssSyntax.REPEAT => arguments.Count == 2
                && arguments[0] is [{ Kind: CssTermKind.Integer, Number: >= 1 and <= CssRuleLimits.MAX_REPEAT }],
            CssSyntax.MINMAX => arguments.Count == 2,
            CssSyntax.ROTATE => arguments is [[var angle]]
                && ((angle.Kind == CssTermKind.Dimension && angle.Unit == CssSyntax.ANGLE_UNIT)
                    || (angle.Kind == CssTermKind.Function && angle.Name == CssSyntax.MathFunctions[0])),
            CssSyntax.TRANSLATE => arguments.Count is 1 or 2 && arguments.All(IsTranslateArgument),
            CssSyntax.COLOR_MIX => IsColourMix(arguments),
            _ => true,
        };
        if (!valid)
        {
            run.Fail(Pos, name == CssSyntax.COLOR_MIX ? CustomLayoutErrorCodes.CSS_COLOUR : CustomLayoutErrorCodes.CSS_VALUE);
            return null;
        }

        var text = name + "(" + CssRun.Join(arguments.Select(a => CssRun.Join(a.Select(t => t.Text), " ")), ", ") + ")";
        return new CssTerm(CssTermKind.Function, text, name);
    }

    private static bool IsTranslateArgument(List<CssTerm> argument) =>
        argument is [var term]
        && ((term.Kind == CssTermKind.Dimension && CssSyntax.LengthUnits.Contains(term.Unit))
            || (term.Kind == CssTermKind.Integer && term.Number == 0)
            || (term.Kind == CssTermKind.Function && CssSyntax.TranslateFunctions.Contains(term.Name)));

    private static bool IsColourMix(List<List<CssTerm>> arguments) =>
        arguments is
        [
            [{ Kind: CssTermKind.Keyword, Name: CssSyntax.MIX_IN }, { Kind: CssTermKind.Keyword, Name: CssSyntax.MIX_SPACE }],
            [{ Kind: CssTermKind.Function, Name: CssSyntax.VAR, IsToken: true }, { Kind: CssTermKind.Dimension, Unit: CssSyntax.PERCENT_UNIT, Number: >= 0 and <= 100 }],
            [{ Kind: CssTermKind.Function, Name: CssSyntax.VAR, IsToken: true }],
        ];

    // Arguments split at commas, up to the closing ). Pos ends after it.
    private bool ParseArguments(bool math, int depth, string property, out List<List<CssTerm>> arguments)
    {
        arguments = [];
        while (true)
        {
            if (!ParseSequence(math, depth, property, out var terms))
            {
                return false;
            }

            if (Pos >= S.Length || S[Pos] is ';' or '}')
            {
                run.Fail(Pos, CustomLayoutErrorCodes.CSS_MALFORMED);
                return false;
            }

            if (terms.Count == 0 || arguments.Count >= CustomLayoutLimits.MAX_FUNCTION_ARGUMENTS)
            {
                run.Fail(Pos, CustomLayoutErrorCodes.CSS_VALUE);
                return false;
            }

            arguments.Add(terms);
            var close = S[Pos] == ')';
            Pos++;
            if (close)
            {
                return true;
            }
        }
    }

    // var(--name): one argument, no fallback, a token, a font token or a variable the markup binds.
    private CssTerm? ParseVar(string property)
    {
        SkipSpace();
        var start = Pos;
        while (Pos < S.Length && IsIdentChar(S[Pos]))
        {
            Pos++;
        }

        var name = S[start..Pos];
        SkipSpace();
        if (Pos >= S.Length || S[Pos] != ')' || !name.StartsWith("--", StringComparison.Ordinal))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VARIABLE);
            return null;
        }

        var isToken = run.Tokens.Contains(name);
        var isFont = run.FontTokens.Contains(name);
        if (!isToken && !isFont && !run.Bound.Contains(name))
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_VARIABLE);
            return null;
        }

        if (isFont && property != CssSyntax.FONT_PROPERTY)
        {
            run.Fail(start, CustomLayoutErrorCodes.CSS_FONT);
            return null;
        }

        Pos++;
        return new CssTerm(CssTermKind.Function, $"var({name})", CssSyntax.VAR, IsToken: isToken, IsFont: isFont);
    }

    private void SkipSpace()
    {
        while (Pos < S.Length && CssRun.IsSpace(S[Pos]))
        {
            Pos++;
        }
    }
}
