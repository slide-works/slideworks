using SlideWorks.Core.Look;

namespace SlideWorks.UnitTest.Look;

// The built-in templates against SW-2's "Built-in templates" table and house style.
public class TemplateCatalogueTests
{
    [Test]
    public void BuiltInsMatchTheDescription_WhenLoaded_ShouldEqualSw2Table()
    {
        var catalogue = new TemplateCatalogue();
        var talk = catalogue.Find("talk")!;
        var workshop = catalogue.Find("workshop")!;
        var update = catalogue.Find("update")!;

        catalogue.All.Select(t => t.Name).ShouldBe(["talk", "workshop", "update"]);
        catalogue.Default.ShouldBe(talk);

        talk.Layouts.ShouldBe(["title", "divider", "bullets", "two-column", "compare", "big-number", "quote", "image", "chart", "cards", "numbered-cards", "rules", "table", "statement", "links", "stack", "bars", "flow", "levels", "close"]);
        workshop.Layouts.ShouldBe(["title", "divider", "bullets", "two-column", "practice", "compare", "quote", "image", "table", "numbered-cards", "rules", "cards", "statement", "links", "stack", "bars", "flow", "levels", "gantt", "columns-list", "timeline", "loop", "close"]);
        update.Layouts.ShouldBe(["title", "bullets", "big-number", "table", "chart", "gantt", "numbered-cards", "cards", "stack", "bars", "flow", "levels", "close"]);

        talk.Chrome.ShouldBe(new TemplateChrome { Tracker = true, Footer = "author", Progress = true, DeepDivesOnDivider = false, Draft = false });
        workshop.Chrome.ShouldBe(new TemplateChrome { Tracker = true, Footer = "author", Progress = true, DeepDivesOnDivider = true, Draft = true });
        update.Chrome.ShouldBe(new TemplateChrome { Tracker = false, Footer = "date", Progress = true, DeepDivesOnDivider = false, Draft = false });

        talk.Outline[0].ShouldBe("Title with the one-line promise");
        workshop.Outline[0].ShouldBe("Title");
        update.Outline[0].ShouldBe("Title with the headline number");

        foreach (var template in catalogue.All)
        {
            template.HouseStyle.Count.ShouldBe(6, template.Name);
            template.HouseStyle[0].ShouldBe("One headline per slide, written as a sentence that makes the point.");
            template.HouseStyle[5].ShouldBe("Optional material is marked as a deep dive rather than cut.");
            template.Type.ShouldBe(new TemplateType { Head = "Plus Jakarta Sans", Body = "Inter", HeadWeight = 800, HeadTracking = "-0.025em" });
            template.Shape.Radius.ShouldBe(new TemplateRadius { Sm = 10, Md = 12, Lg = 16, Xl = 18 });
            template.Ornament.ShouldBe("dots");
            template.TitleArt.ShouldBeNull();
            template.LockPalette.ShouldBeFalse();
            template.Palette.ShouldBeNull();
        }
    }

    [Test]
    public void Allows_WhenLayoutOutsideTheTemplate_ShouldBeFalse()
    {
        var talk = new TemplateCatalogue().Find("talk")!;

        talk.Allows("chart").ShouldBeTrue();
        talk.Allows("gantt").ShouldBeFalse();
    }

    [Test]
    public void Allows_WhenQa_ShouldBeTrue()
    {
        var talk = new TemplateCatalogue().Find("talk")!;

        talk.Allows("qa").ShouldBeTrue();
        talk.Allows("join").ShouldBeTrue();
        talk.Allows("poll").ShouldBeTrue();
    }
}
