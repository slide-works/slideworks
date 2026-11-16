using System.Globalization;
using System.Text;

namespace SlideWorks.Core.Slides;

// Input echoed back in an error (an unknown field name, an enum value, a layout name): cut to
// MAX_LENGTH UTF-16 characters and then "…", with control and line-separator characters escaped
// (\n, \u0000), so every error stays one short "<path>: <message>" line whatever the input was.
internal static class EchoText
{
    public const int MAX_LENGTH = 40;

    public static string Of(string text)
    {
        var echo = new StringBuilder();
        var cut = text.Length > MAX_LENGTH;
        var end = cut && char.IsHighSurrogate(text[MAX_LENGTH - 1]) ? MAX_LENGTH - 1 : Math.Min(text.Length, MAX_LENGTH);
        foreach (var c in text.AsSpan(0, end))
        {
            echo.Append(Escape(c));
        }

        return cut ? echo.Append('…').ToString() : echo.ToString();
    }

    private static string Escape(char c) => c switch
    {
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ when char.IsControl(c) || c is '\u2028' or '\u2029' => string.Create(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}"),
        _ => c.ToString(),
    };
}
