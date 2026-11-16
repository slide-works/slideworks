using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

// The built-in templates plus "locked", a template that brings and locks the ember palette. No
// built-in locks its palette, so LookRulesTests needs this to reach the locking rules.
public sealed class LockingTemplates : ITemplateCatalogue
{
    private readonly TemplateCatalogue _builtIns = new();

    public IReadOnlyList<Template> All => [.. _builtIns.All, _builtIns.Default with { Name = "locked", Label = "Locked", IsDefault = false, Palette = "ember", LockPalette = true }];

    public Template Default => _builtIns.Default;

    public Template? Find(string name) => All.FirstOrDefault(t => t.Name == name);
}
