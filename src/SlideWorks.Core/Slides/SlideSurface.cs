namespace SlideWorks.Core.Slides;

// The surface one slide is drawn on (K7): the layout's own when it has only one, else the one the
// slide asks for, light when it asks for none.
public static class SlideSurface
{
    public const string LIGHT = "light";

    public const string DARK = "dark";

    // Returns "light" or "dark". A surface the layout cannot take, or a word that is neither, is
    // recorded as a failure and the layout's own surface is returned.
    public static string Resolve(LayoutDefinition definition, string? requested, SlotValidation validation)
    {
        var own = definition.Surface == LayoutSurface.Dark ? DARK : LIGHT;
        if (requested is null)
        {
            return own;
        }

        if (!string.Equals(requested, LIGHT, StringComparison.Ordinal) && !string.Equals(requested, DARK, StringComparison.Ordinal))
        {
            validation.Fail("surface", "use 'light' or 'dark'");
            return own;
        }

        if (definition.Surface == LayoutSurface.Either || string.Equals(requested, own, StringComparison.Ordinal))
        {
            return requested;
        }

        validation.Fail($"{definition.Name}.surface", $"this layout is {own} only");
        return own;
    }
}
