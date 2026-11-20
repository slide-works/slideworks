using System.Text.RegularExpressions;

namespace SlideWorks.Core.Look.Manifest;

// The check on shape.shadow. A custom layout's CSS may reach this value through a token, so it is refused unless it
// is "none" or a comma-separated list of shadows, each two to four lengths (a number with px, or 0) and one rgb()
// colour whose arguments hold only numbers, spaces, commas, / and %. So url(, var( and every other function are out.
public static partial class ManifestShadow
{
    public const int MAX_LENGTH = 200;

    public static bool IsSafe(string shadow)
    {
        if (shadow == "none")
        {
            return true;
        }

        if (shadow.Length > MAX_LENGTH || !Allowed().IsMatch(shadow))
        {
            return false;
        }

        // With the colours taken out no comma is left inside brackets, so a comma separates two shadows.
        return Colour().Replace(shadow, "C").Split(',').All(IsOneShadow);
    }

    private static bool IsOneShadow(string shadow)
    {
        var parts = shadow.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var colours = parts.Count(part => part == "C");
        var lengths = parts.Count(part => Length().IsMatch(part));
        return colours == 1 && lengths is >= 2 and <= 4 && colours + lengths == parts.Length;
    }

    // Only the characters a length or an rgb() colour is made of: cheap, and nothing else can hide in the value.
    [GeneratedRegex(@"\A[0-9a-z., /%()\-]+\z")]
    private static partial Regex Allowed();

    [GeneratedRegex(@"rgb\([0-9., /%]+\)")]
    private static partial Regex Colour();

    [GeneratedRegex(@"\A(0|-?([0-9]+(\.[0-9]+)?|\.[0-9]+)px)\z")]
    private static partial Regex Length();
}
