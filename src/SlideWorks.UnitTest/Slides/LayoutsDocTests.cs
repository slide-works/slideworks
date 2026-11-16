namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class LayoutsDocTests
{
    [Test]
    public void LayoutsDoc_ShouldMatchCatalogue()
    {
        var path = ContractFiles.PathOf("layouts.md");
        var rendered = LayoutsDoc.Render().ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable("UPDATE_LAYOUTS_DOC") == "1")
        {
            File.WriteAllText(path, rendered);
        }

        File.ReadAllText(path).ReplaceLineEndings("\n").ShouldBe(rendered,
            "src/contracts/layouts.md is out of date: rerun this test with UPDATE_LAYOUTS_DOC=1 and commit the file");
    }
}
