using System.Globalization;
using System.Text.RegularExpressions;

namespace SlideWorks.Core.Look;

// WCAG 2.x contrast (L2) between two opaque CSS colours, written as #rgb, #rrggbb or rgb(r g b).
public static partial class Contrast
{
    public static double Ratio(string fg, string bg)
    {
        var a = Luminance(Parse(fg));
        var b = Luminance(Parse(bg));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    // fg mixed over bg in sRGB at share (0 to 1): share 0 is bg, share 1 is fg. Returns #rrggbb.
    public static string Mix(string fg, string bg, double share)
    {
        var f = Parse(fg);
        var b = Parse(bg);
        int Channel(int from, int to) => (int)Math.Round((from * share) + (to * (1 - share)), MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"#{Channel(f.R, b.R):x2}{Channel(f.G, b.G):x2}{Channel(f.B, b.B):x2}");
    }

    public static bool IsColour(string value) => TryParse(value, out _);

    private static (int R, int G, int B) Parse(string colour) =>
        TryParse(colour, out var rgb) ? rgb : throw new FormatException($"Not an opaque CSS colour: '{colour}'");

    private static bool TryParse(string value, out (int R, int G, int B) rgb)
    {
        rgb = default;
        var hex = HexColour().Match(value);
        if (hex.Success)
        {
            var digits = hex.Groups[1].Value;
            if (digits.Length == 3)
            {
                digits = string.Concat(digits.Select(c => new string(c, 2)));
            }

            rgb = (Byte(digits, 0), Byte(digits, 2), Byte(digits, 4));
            return true;
        }

        var func = RgbColour().Match(value);
        if (!func.Success)
        {
            return false;
        }

        var channels = new[] { 1, 2, 3 }.Select(i => int.Parse(func.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
        if (channels.Any(c => c > 255))
        {
            return false;
        }

        rgb = (channels[0], channels[1], channels[2]);
        return true;
    }

    private static int Byte(string hex, int start) => int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double Luminance((int R, int G, int B) rgb) =>
        (0.2126 * Linear(rgb.R)) + (0.7152 * Linear(rgb.G)) + (0.0722 * Linear(rgb.B));

    private static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"^rgb\((\d{1,3}) (\d{1,3}) (\d{1,3})\)$")]
    private static partial Regex RgbColour();
}
