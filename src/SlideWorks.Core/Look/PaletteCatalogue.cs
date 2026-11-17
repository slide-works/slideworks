namespace SlideWorks.Core.Look;

// The built-in palettes (L1, L3): cobalt (the default), ember, plum and midnight, from the embedded
// src/contracts/palettes.json.
public sealed class PaletteCatalogue : IPaletteCatalogue
{
    private static readonly Lazy<IReadOnlyList<Palette>> _all = new(() => LookJson.Read<PaletteFile>("palettes.json").Palettes);

    public IReadOnlyList<Palette> All => _all.Value;

    public Palette Default => All.Single(p => p.IsDefault);

    public Palette? Find(string name) => All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
}
