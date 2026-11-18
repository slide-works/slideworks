namespace SlideWorks.Core.Look.Manifest;

// A manifest's counts against TemplateLimits, and the token name of each ornament dot. An empty list means pass.
public static class ManifestLimits
{
    // The only names a slide draws a dot in.
    public static IReadOnlyList<string> DotNames { get; } = ["accent", "accent-2", "accent-glow", "good", "warn", "bad", .. ExtraColours.Names];

    public static IReadOnlyList<ManifestFinding> Check(TemplateManifest manifest)
    {
        var findings = new List<ManifestFinding>();
        if (manifest.Fonts.Count > TemplateLimits.MAX_FONT_FILES)
        {
            findings.Add(ManifestFinding.Of("too_many_fonts", "fonts"));
        }

        if (manifest.CustomLayouts.Count > TemplateLimits.MAX_CUSTOM_LAYOUTS)
        {
            findings.Add(ManifestFinding.Of("too_many_layouts", "customLayouts"));
        }

        var dots = manifest.Ornament.Dots;
        if (dots.Count > TemplateLimits.MAX_ORNAMENT_DOTS)
        {
            // Over the limit the names are not looked at, so the answer stays small however long the list is.
            findings.Add(ManifestFinding.Invalid("ornament.dots", $"can hold at most {TemplateLimits.MAX_ORNAMENT_DOTS} dots"));
            return findings;
        }

        findings.AddRange(dots
            .Select((dot, index) => (dot, index))
            .Where(entry => !DotNames.Contains(entry.dot))
            .Select(entry => ManifestFinding.Invalid($"ornament.dots[{entry.index}]", "must be one of: " + string.Join(", ", DotNames))));
        return findings;
    }
}
