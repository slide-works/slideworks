using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Templates;

// The in-memory pair of catalogues Core's LookRules takes (T4), made from templates ITemplates resolved. Callers hold the
// result in var, so they never name a catalogue type.
public static class ResolvedCatalogues
{
    // The reserved palette name: the palette a template brings is listed under it, and no built-in palette has it.
    public const string OWN_PALETTE = "template";

    // The templates are the resolved ones, found by ref or name, and the first is the default. The palettes are the
    // built-in ones plus each resolved template's own palette under the reserved name.
    public static (ITemplateCatalogue Templates, IPaletteCatalogue Palettes) For(IPaletteCatalogue builtIn, params ResolvedTemplate[] resolved)
    {
        if (resolved.Length == 0)
        {
            throw new ArgumentException("At least one resolved template is needed: the first is the default.", nameof(resolved));
        }
        var own = resolved.Where(r => r.OwnPalette is not null).Select(r => r.OwnPalette! with { Name = OWN_PALETTE, IsDefault = false });
        return (new Templates(resolved), new Palettes(builtIn.Default, [.. builtIn.All, .. own]));
    }

    private sealed class Templates(IReadOnlyList<ResolvedTemplate> resolved) : ITemplateCatalogue
    {
        public IReadOnlyList<Template> All { get; } = [.. resolved.Select(r => r.Template)];

        public Template Default => resolved[0].Template;

        public Template? Find(string name)
            => resolved.FirstOrDefault(r => string.Equals(r.Ref, name, StringComparison.Ordinal) || string.Equals(r.Template.Name, name, StringComparison.Ordinal))?.Template;
    }

    private sealed class Palettes(Palette byDefault, IReadOnlyList<Palette> all) : IPaletteCatalogue
    {
        public IReadOnlyList<Palette> All => all;

        public Palette Default => byDefault;

        public Palette? Find(string name) => all.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
    }
}
