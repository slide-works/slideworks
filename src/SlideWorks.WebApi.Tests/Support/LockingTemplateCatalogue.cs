using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Tests.Support;

// The built-in templates plus one that locks its palette (L3), which no built-in does. Swap it in with
// ConfigureTestServices: services.AddSingleton<ITemplateCatalogue, LockingTemplateCatalogue>(). The locking template is
// the default template's structure under another name, locked to a palette other than the default one.
public sealed class LockingTemplateCatalogue : ITemplateCatalogue
{
    public const string LOCKING = "locked";

    private readonly TemplateCatalogue _builtIns = new();
    private readonly IReadOnlyList<Template> _all;

    public LockingTemplateCatalogue()
    {
        var palettes = new PaletteCatalogue();
        Palette = palettes.All.First(p => !p.IsDefault).Name;
        _all = [.. _builtIns.All, _builtIns.Default with { Name = LOCKING, Label = "Locked", IsDefault = false, Palette = Palette, LockPalette = true }];
    }

    // The palette the locking template forces.
    public string Palette { get; }

    public IReadOnlyList<Template> All => _all;

    public Template Default => _builtIns.Default;

    public Template? Find(string name) => _all.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
}
