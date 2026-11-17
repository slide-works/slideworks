using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Tests.Templates;

// The catalogues Core's LookRules is given, made from resolved templates (T4).
[TestFixture]
public class ResolvedCataloguesTests
{
    private static readonly TemplateCatalogue _templates = new();
    private static readonly PaletteCatalogue _palettes = new();

    [Test]
    public void For_WhenATemplateHasItsOwnPalette_ShouldListItAsTemplate()
    {
        var brand = _palettes.Default with { Name = "northwind", Label = "Northwind", IsDefault = true };
        var template = _templates.Default with { Name = "tpl_northwind", IsDefault = false, Palette = ResolvedCatalogues.OWN_PALETTE, LockPalette = true };
        var resolved = new ResolvedTemplate(template, "tpl_northwind", 2, false) { OwnPalette = brand };

        var catalogues = ResolvedCatalogues.For(_palettes, resolved);

        var own = catalogues.Palettes.Find("template");
        own.ShouldNotBeNull();
        own.Label.ShouldBe("Northwind");
        own.IsDefault.ShouldBeFalse();
        catalogues.Palettes.All.Select(p => p.Name).ShouldBe([.. _palettes.All.Select(p => p.Name), "template"]);
        catalogues.Palettes.Default.ShouldBe(_palettes.Default);
        catalogues.Templates.Default.ShouldBe(template);
        var look = new LookRules(catalogues.Templates, catalogues.Palettes).Resolve("tpl_northwind", null);
        look.Palette.ShouldBe(own);
    }

    [Test]
    public void For_WhenGivenABuiltIn_ShouldFindItByName()
    {
        var workshop = _templates.All.First(t => !t.IsDefault);

        var catalogues = ResolvedCatalogues.For(_palettes, BuiltInTemplates.Resolve(_templates.Default), BuiltInTemplates.Resolve(workshop));

        catalogues.Templates.Find(workshop.Name).ShouldBe(workshop);
        catalogues.Templates.Find("no-such-template").ShouldBeNull();
        catalogues.Templates.Default.ShouldBe(_templates.Default);
        catalogues.Templates.All.ShouldBe([_templates.Default, workshop]);
        catalogues.Palettes.All.ShouldBe(_palettes.All);
    }

    [Test]
    public void For_WhenGivenNoTemplate_ShouldRefuse()
    {
        Should.Throw<ArgumentException>(() => ResolvedCatalogues.For(_palettes)).ParamName.ShouldBe("resolved");
    }
}
