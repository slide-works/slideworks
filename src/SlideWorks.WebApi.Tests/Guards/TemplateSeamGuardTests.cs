namespace SlideWorks.WebApi.Tests.Guards;

// Core's template catalogue is named only inside Features/Templates, by the class that registers it, and by the callers
// on the allow-list (T4). Everything else asks ITemplates, so an uploaded template reaches it.
[TestFixture]
public class TemplateSeamGuardTests
{
    private const string CATALOGUE = "ITemplateCatalogue";

    private static readonly string[] _allowed = [.. TemplateSeamAllowList.Slides, .. TemplateSeamAllowList.Decks, .. TemplateSeamAllowList.Look];

    [Test]
    public void Catalogue_WhenInjectedOutsideTemplates_ShouldBeOnTheAllowList()
    {
        var naming = Outside().Where(file => NamesTheCatalogue(File.ReadAllLines(Path.Combine(ProductionSource.WebApiDirectory(), file)))).ToList();

        Outside().ShouldNotBeEmpty();
        naming.Except(_allowed, StringComparer.Ordinal).ShouldBeEmpty();
    }

    // The list only holds callers that are still there: a file that stopped naming the catalogue comes off it.
    [Test]
    public void AllowList_WhenAFileNoLongerNamesTheCatalogue_ShouldNotListIt()
    {
        var naming = Outside().Where(file => NamesTheCatalogue(File.ReadAllLines(Path.Combine(ProductionSource.WebApiDirectory(), file)))).ToList();

        _allowed.Except(naming, StringComparer.Ordinal).ShouldBeEmpty();
        _allowed.ShouldBeUnique();
    }

    [TestCase("public class DeckReader(AppDbContext db, ITemplateCatalogue templates)")]
    [TestCase("    ITemplateCatalogue templates,")]
    [TestCase("var x = 1; // then ask ITemplateCatalogue")]
    public void Guard_WhenALineOfCodeNamesTheCatalogue_ShouldFlagIt(string line)
    {
        NamesTheCatalogue([line]).ShouldBeTrue();
    }

    [TestCase("// Tests swap ITemplateCatalogue for another.")]
    [TestCase("    // read through ITemplateCatalogue so nothing here names a template")]
    [TestCase("public class DeckReader(AppDbContext db, ITemplates templates)")]
    public void Guard_WhenOnlyACommentLineNamesTheCatalogue_ShouldNotFlagIt(string line)
    {
        NamesTheCatalogue([line]).ShouldBeFalse();
    }

    // Every production file outside Features/Templates and the registration class, as a path relative to the project.
    private static List<string> Outside()
    {
        var root = ProductionSource.WebApiDirectory();
        return ProductionSource.Matching(["**/*.cs"])
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(file => !file.StartsWith("Features/Templates/", StringComparison.Ordinal) && file != "Features/Look/CoreCatalogueRegistrations.cs")
            .ToList();
    }

    // A comment line (one that starts with // after its indent) does not count.
    private static bool NamesTheCatalogue(IEnumerable<string> lines)
        => lines.Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && line.Contains(CATALOGUE, StringComparison.Ordinal));
}
