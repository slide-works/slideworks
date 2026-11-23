using System.Globalization;

namespace SlideWorks.Core.Slides;

// How many "average characters" one character costs when SlideFit wraps text. SlideFit counts a line's
// capacity in average glyphs (the em a caller passes: FitWidths.Body/Head/Cell, 0.56 for the built-in
// fonts); a character that draws wider than the average costs more of that capacity. No factor is below
// 1.0, so text that wrapped to N lines before never wraps to fewer, and ordinary lowercase prose keeps
// the limits it had (narrow letters are not credited for being narrow).
//
// The numbers come from the advance widths of the fonts the client ships (@fontsource/inter 5.3.0 and
// @fontsource/plus-jakarta-sans, latin subsets, weights 600 and 700, the heaviest the slides use), read
// from the font files' hmtx tables, taking the larger of Inter and Plus Jakarta Sans per character. Each
// factor is that width divided by 0.56 em, rounded up, so no character is charged less than it draws:
// % 1.046 em (1.868 units), W 1.038 (1.853), @ 1.016 (1.814), M 0.932 (1.664), m 0.924 (1.650),
// # 0.915 (1.634), w 0.883 (1.577), O and Q 0.878 (1.568), G 0.813 (1.452), & 0.793 (1.416),
// C N H A V D X U Y K 0.719 to 0.777 (up to 1.387), every other capital up to 0.692 (B, 1.236),
// 0 0.716 (1.279), the other digits up to 0.676 (1.208), + < = > ~ 0.679 (1.213), $ ^ _ up to 0.663.
// Lowercase letters other than m and w stay at 1.0: body text is measured at 0.56 em although its
// lowercase averages 0.47 em, so that slack already covers b, d, n, o and the like.
// A template's own fonts reach SlideFit as a different average em in the same call, so they scale
// through that em and need no factors of their own.
internal static class GlyphWidths
{
    private const double WIDEST = 1.87;
    private const double AT_SIGN = 1.82;
    private const double WIDE = 1.67;
    private const double WIDE_ROUND = 1.58;
    private const double CAPITAL_G = 1.46;
    private const double CAPITAL_BROAD = 1.39;
    private const double CAPITAL = 1.24;
    private const double ZERO = 1.28;
    private const double DIGIT = 1.22;

    public static double Factor(string textElement)
    {
        var c = textElement[0];
        return c switch
        {
            '%' or 'W' => WIDEST,
            '@' => AT_SIGN,
            'M' or 'm' or '#' => WIDE,
            'w' or 'O' or 'Q' => WIDE_ROUND,
            'G' or '&' => CAPITAL_G,
            'C' or 'N' or 'H' or 'A' or 'V' or 'D' or 'X' or 'U' or 'Y' or 'K' => CAPITAL_BROAD,
            >= 'A' and <= 'Z' => CAPITAL,
            '0' => ZERO,
            >= '1' and <= '9' or '+' or '<' or '=' or '>' or '~' or '$' or '^' or '_' => DIGIT,
            _ => 1.0,
        };
    }

    // What a word costs: the sum of its characters' factors.
    public static double Cost(string word)
    {
        var total = 0.0;
        var elements = StringInfo.GetTextElementEnumerator(word);
        while (elements.MoveNext())
        {
            total += Factor((string)elements.Current);
        }

        return total;
    }
}
