namespace SlideWorks.Core.Look;

// The outcome of LookRules.Resolve: a template and a palette, or an error with an L3 text.
public sealed record LookResult(Template? Template, Palette? Palette, string? Error)
{
    public static LookResult Ok(Template template, Palette palette) => new(template, palette, null);

    public static LookResult Fail(string error) => new(null, null, error);
}
