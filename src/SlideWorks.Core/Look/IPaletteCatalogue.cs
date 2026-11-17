namespace SlideWorks.Core.Look;

// The palettes a deck can use (L3). PaletteCatalogue holds the built-ins.
public interface IPaletteCatalogue
{
    IReadOnlyList<Palette> All { get; }

    Palette Default { get; }

    Palette? Find(string name);
}
