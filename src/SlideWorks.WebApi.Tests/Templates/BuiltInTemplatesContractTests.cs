using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Tests.Templates;

// The contract over the built-in catalogue, with nothing else behind it.
[TestFixture]
public class BuiltInTemplatesContractTests : TemplatesContractTests
{
    protected override ITemplates Create() => new BuiltInTemplates(BuiltIns);

    [Test]
    public async Task Find_WhenTheNameIsUnknown_ShouldAnswerNull()
    {
        var found = await Create().FindAsync("no-such-template", null, CancellationToken.None);

        found.ShouldBeNull();
    }

    [Test]
    public void OfBuiltIn_WhenTheTemplateHasTitleArt_ShouldNameItsGenerator()
    {
        var withArt = BuiltIns.All.FirstOrDefault(t => t.TitleArt != null) ?? BuiltIns.Default with { TitleArt = "dots" };

        var look = TemplateLook.OfBuiltIn(withArt);

        look.ShouldBe(TemplateLook.None with { ArtKind = "generator", ArtGenerator = withArt.TitleArt });
        TemplateLook.OfBuiltIn(withArt with { TitleArt = null }).ShouldBe(TemplateLook.None);
        TemplateLook.None.ArtKind.ShouldBe("none");
        TemplateLook.None.TopBar.ShouldBe("none");
    }
}
