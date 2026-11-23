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
// from the font files' hmtx tables and divided by 0.56 em, taking the larger of Inter and Plus Jakarta
// Sans per character and then rounding each class up: W 1.04 em, M 0.93 em, w 0.88 em, m 0.92 em,
// % 1.05 em, @ 1.02 em; the other capitals average 0.70 em (up to 0.88 em for O and Q); digits average
// 0.63 em. A template's own fonts reach SlideFit as a different average em in the same call, so they
// scale through that em and need no factors of their own.
internal static class GlyphWidths
{
    private const double WIDE = 1.85;
    private const double WIDE_LOWERCASE = 1.62;
    private const double CAPITAL = 1.24;
    private const double DIGIT = 1.12;

    public static double Factor(string textElement)
    {
        var c = textElement[0];
        return c switch
        {
            'W' or 'M' or '%' or '@' => WIDE,
            'w' or 'm' => WIDE_LOWERCASE,
            >= 'A' and <= 'Z' => CAPITAL,
            >= '0' and <= '9' => DIGIT,
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
