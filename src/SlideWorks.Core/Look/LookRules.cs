namespace SlideWorks.Core.Look;

// Which template and palette a deck gets (L3, D9). The error texts are frozen.
public sealed class LookRules(ITemplateCatalogue templates, IPaletteCatalogue palettes)
{
    public static string UnknownTemplate(string name) => $"Unknown template '{name}'";

    public static string UnknownPalette(string name) => $"Unknown palette '{name}'";

    public static string LocksItsPalette(string name) => $"Template '{name}' locks its palette";

    // The look of a new deck. With no template, the default template; with no palette, the template's
    // own palette, else the default palette. A template that locks its palette forces it.
    public LookResult Resolve(string? templateName, string? paletteName)
    {
        var template = templateName is null ? templates.Default : templates.Find(templateName);
        if (template is null)
        {
            return LookResult.Fail(UnknownTemplate(templateName!));
        }

        var error = CheckPalette(template, paletteName);
        if (error is not null)
        {
            return LookResult.Fail(error);
        }

        var palette = PaletteAfter(template, paletteName ?? template.Palette ?? palettes.Default.Name, new LookChange());
        return LookResult.Ok(template, palettes.Find(palette) ?? throw new InvalidOperationException(UnknownPalette(palette)));
    }

    // Null when the change is allowed for a deck now on currentTemplate, else the error text.
    public string? CheckChange(Template currentTemplate, LookChange change)
    {
        var target = currentTemplate;
        if (change.Template is not null)
        {
            target = templates.Find(change.Template);
            if (target is null)
            {
                return UnknownTemplate(change.Template);
            }
        }

        return CheckPalette(target, change.Palette);
    }

    // The palette a deck has after an allowed change to target: a locking template's own palette,
    // else the change's palette, else the one it had.
    public static string PaletteAfter(Template target, string currentPalette, LookChange change) =>
        target.LockPalette ? target.Palette! : change.Palette ?? currentPalette;

    private string? CheckPalette(Template template, string? paletteName)
    {
        if (paletteName is null)
        {
            return null;
        }

        if (palettes.Find(paletteName) is null)
        {
            return UnknownPalette(paletteName);
        }

        return template.LockPalette && !string.Equals(paletteName, template.Palette, StringComparison.Ordinal)
            ? LocksItsPalette(template.Name)
            : null;
    }
}
