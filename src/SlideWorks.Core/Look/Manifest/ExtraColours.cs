namespace SlideWorks.Core.Look.Manifest;

// The extra colours of a manifest's palette: at most TemplateLimits.MAX_EXTRAS, named extra-1 to extra-4, each once,
// each with an ink that reads on it. An empty list means pass.
public static class ExtraColours
{
    // The bar of normal text (WCAG AA), the one PaletteContrast holds its text pairs to.
    public const double INK_BAR = 4.5;

    public static IReadOnlyList<string> Names { get; } =
        Enumerable.Range(1, TemplateLimits.MAX_EXTRAS).Select(number => $"extra-{number}").ToList();

    public static IReadOnlyList<ManifestFinding> Check(TemplateManifest manifest)
    {
        if (manifest.Palette is not { } palette)
        {
            return [];
        }

        // Over the limit nothing else is looked at, so the answer stays small however long the list is.
        if (palette.Extras.Count > TemplateLimits.MAX_EXTRAS)
        {
            return [ManifestFinding.Of("too_many_extras", "palette.extras")];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return palette.Extras.SelectMany((extra, index) => CheckOne(extra, $"palette.extras[{index}]", seen)).ToList();
    }

    private static IEnumerable<ManifestFinding> CheckOne(ManifestExtra extra, string path, HashSet<string> seen)
    {
        if (!Names.Contains(extra.Name))
        {
            yield return ManifestFinding.Invalid(path + ".name", "must be one of: " + string.Join(", ", Names));
        }
        else if (!seen.Add(extra.Name))
        {
            yield return ManifestFinding.Invalid(path + ".name", $"repeats {extra.Name}; each extra colour can be given once");
        }

        var colour = ManifestJson.IsHexColour(extra.Colour);
        var ink = ManifestJson.IsHexColour(extra.Ink);
        if (!colour)
        {
            yield return ManifestFinding.Invalid(path + ".colour", ManifestJson.HEX_RULE);
        }

        if (!ink)
        {
            yield return ManifestFinding.Invalid(path + ".ink", ManifestJson.HEX_RULE);
        }

        if (colour && ink && Contrast.Ratio(extra.Ink, extra.Colour) is var ratio && ratio < INK_BAR)
        {
            // The pair is one of our own names, never text the manifest gave.
            yield return ManifestFinding.Contrast(path + ".ink", Names.Contains(extra.Name) ? extra.Name : "extra colour", ratio, INK_BAR);
        }
    }
}
