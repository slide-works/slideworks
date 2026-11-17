namespace SlideWorks.Core.Look;

// The built-in templates (L1, L3): talk (the default), workshop and update, from the embedded
// src/contracts/templates.json.
public sealed class TemplateCatalogue : ITemplateCatalogue
{
    private static readonly Lazy<IReadOnlyList<Template>> _all = new(() => LookJson.Read<TemplateFile>("templates.json").Templates);

    public IReadOnlyList<Template> All => _all.Value;

    public Template Default => All.Single(t => t.IsDefault);

    public Template? Find(string name) => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
}
