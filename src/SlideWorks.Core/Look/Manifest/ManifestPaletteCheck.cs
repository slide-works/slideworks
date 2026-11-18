namespace SlideWorks.Core.Look.Manifest;

// A manifest's palette against the contrast pairs every built-in palette passes (PaletteContrast). An empty list
// means pass. It never changes or suggests a colour. A locked palette needs a palette to lock.
public static class ManifestPaletteCheck
{
    public static IReadOnlyList<ManifestFinding> Check(TemplateManifest manifest)
    {
        if (manifest.Palette is not { } palette)
        {
            return manifest.LockPalette
                ? [ManifestFinding.Invalid("lockPalette", "is true, so the template must bring a palette")]
                : [];
        }

        // ManifestJson.Parse has checked this already; a manifest built in code has not been through it.
        var unreadable = ManifestPalette.Keys
            .Where(key => !palette.Tokens.TryGetValue(key, out var colour) || !ManifestJson.IsHexColour(colour))
            .Select(key => ManifestFinding.Invalid("palette." + key, ManifestJson.HEX_RULE))
            .ToList();
        if (unreadable.Count > 0)
        {
            return unreadable;
        }

        var pairs = PaletteContrast.Pairs;
        var tokens = new Palette { Name = manifest.Name, Label = manifest.Label, IsDefault = false, Words = [], Swatch = [], Tokens = palette.Tokens };
        return PaletteContrast.Check(tokens)
            .Select((check, index) => (Check: check, pairs[index].Fore))
            .Where(entry => !entry.Check.Pass)
            .Select(entry => ManifestFinding.Contrast("palette." + entry.Fore, entry.Check.Pair, entry.Check.Ratio, entry.Check.Bar))
            .ToList();
    }
}
