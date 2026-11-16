using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

public class LookRulesTests
{
    [Test]
    public void DefaultsLockingAndErrors_WhenCatalogueHasALockingTemplate_ShouldFollowL3()
    {
        var templates = new LockingTemplates();
        var rules = new LookRules(templates, new PaletteCatalogue());
        var talk = templates.Find("talk")!;
        var locked = templates.Find("locked")!;

        // Defaults: talk and cobalt; a template's own palette when it has one.
        Names(rules.Resolve(null, null)).ShouldBe(("talk", "cobalt"));
        Names(rules.Resolve("workshop", null)).ShouldBe(("workshop", "cobalt"));
        Names(rules.Resolve(null, "plum")).ShouldBe(("talk", "plum"));
        Names(rules.Resolve("locked", null)).ShouldBe(("locked", "ember"));
        Names(rules.Resolve("locked", "ember")).ShouldBe(("locked", "ember"));

        // Errors, with the frozen texts.
        rules.Resolve("keynote", null).Error.ShouldBe("Unknown template 'keynote'");
        rules.Resolve(null, "teal").Error.ShouldBe("Unknown palette 'teal'");
        rules.Resolve("locked", "plum").Error.ShouldBe("Template 'locked' locks its palette");

        // Changes.
        rules.CheckChange(talk, new LookChange(Palette: "midnight")).ShouldBeNull();
        rules.CheckChange(talk, new LookChange(Template: "locked")).ShouldBeNull();
        rules.CheckChange(talk, new LookChange(Template: "keynote")).ShouldBe("Unknown template 'keynote'");
        rules.CheckChange(talk, new LookChange(Palette: "teal")).ShouldBe("Unknown palette 'teal'");
        rules.CheckChange(locked, new LookChange(Palette: "plum")).ShouldBe("Template 'locked' locks its palette");
        rules.CheckChange(talk, new LookChange(Template: "locked", Palette: "plum")).ShouldBe("Template 'locked' locks its palette");
        rules.CheckChange(locked, new LookChange(Template: "talk", Palette: "plum")).ShouldBeNull();

        // Switching to a locking template switches the palette too (D9).
        LookRules.PaletteAfter(locked, "cobalt", new LookChange(Template: "locked")).ShouldBe("ember");
        LookRules.PaletteAfter(talk, "ember", new LookChange(Template: "talk")).ShouldBe("ember");
        LookRules.PaletteAfter(talk, "ember", new LookChange(Palette: "plum")).ShouldBe("plum");
    }

    private static (string?, string?) Names(LookResult result)
    {
        result.Error.ShouldBeNull();
        return (result.Template?.Name, result.Palette?.Name);
    }
}
