namespace SlideWorks.Core.Look.Manifest;

// The file fields of a manifest. Storage and bundles walk them only through Refs and With, so a new file field is
// added here once.
public static class ManifestFiles
{
    public const string FONT = "font";
    public const string LOGO = "logo";
    public const string ART = "art";
    public const string LAYOUT = "layout";

    // Every file field that is set, in file order.
    public static IReadOnlyList<ManifestFileRef> Refs(TemplateManifest manifest)
    {
        var refs = new List<ManifestFileRef>();
        for (var i = 0; i < manifest.Fonts.Count; i++)
        {
            refs.Add(new ManifestFileRef($"fonts[{i}].file", FONT, manifest.Fonts[i].File));
        }

        if (manifest.Logos.Light is { } light)
        {
            refs.Add(new ManifestFileRef("logos.light", LOGO, light));
        }

        if (manifest.Logos.Dark is { } dark)
        {
            refs.Add(new ManifestFileRef("logos.dark", LOGO, dark));
        }

        if (manifest.TitleArt.File is { } art)
        {
            refs.Add(new ManifestFileRef("titleArt.file", ART, art));
        }

        for (var i = 0; i < manifest.CustomLayouts.Count; i++)
        {
            refs.Add(new ManifestFileRef($"customLayouts[{i}].file", LAYOUT, manifest.CustomLayouts[i].File));
        }

        return refs;
    }

    // A copy with every file field's value replaced by what the map holds for it. A value the map lacks throws
    // KeyNotFoundException: the caller checks Refs first.
    public static TemplateManifest With(TemplateManifest manifest, IReadOnlyDictionary<string, string> map) =>
        manifest with
        {
            Fonts = manifest.Fonts.Select(font => font with { File = map[font.File] }).ToList(),
            Logos = manifest.Logos with
            {
                Light = manifest.Logos.Light is { } light ? map[light] : null,
                Dark = manifest.Logos.Dark is { } dark ? map[dark] : null,
            },
            TitleArt = manifest.TitleArt with { File = manifest.TitleArt.File is { } art ? map[art] : null },
            CustomLayouts = manifest.CustomLayouts.Select(layout => layout with { File = map[layout.File] }).ToList(),
        };
}
