using Agoda.IoC.Core;
using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look;

// Registers Core's built-in catalogues (L3) with Agoda.IoC, which only scans this assembly. Core stays free of DI
// attributes, so this one class stands in for both. Tests swap ITemplateCatalogue for LockingTemplateCatalogue through
// ConfigureTestServices.
[RegisterSingleton(For = typeof(ITemplateCatalogue))]
[RegisterSingleton(For = typeof(IPaletteCatalogue))]
public sealed class CoreCatalogueRegistrations : ITemplateCatalogue, IPaletteCatalogue
{
    private readonly TemplateCatalogue _templates = new();
    private readonly PaletteCatalogue _palettes = new();

    IReadOnlyList<Template> ITemplateCatalogue.All => _templates.All;

    Template ITemplateCatalogue.Default => _templates.Default;

    Template? ITemplateCatalogue.Find(string name) => _templates.Find(name);

    IReadOnlyList<Palette> IPaletteCatalogue.All => _palettes.All;

    Palette IPaletteCatalogue.Default => _palettes.Default;

    Palette? IPaletteCatalogue.Find(string name) => _palettes.Find(name);
}
