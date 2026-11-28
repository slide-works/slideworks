using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Tests.Guards;

// A slide's translated lines and a deck's translation settings (SW-883 K2) are written only by Features/Translations and by
// the two slide files that carry the lines with a slide (SlideEdits, SlideCommands). A line elsewhere that assigns
// TranslationsJson or writes a settings row, through EF or raw SQL, fails here by naming the file. The model configuration
// and the migrations are the only other files that name them. The test seed is under the tests, not production source.
[TestFixture]
public class TranslationDataGuardTests
{
    private static readonly Regex _translationWrite = new(
        @"\bTranslationsJson\s*=(?!=)"
        + @"|new\s+DeckTranslationSettingsRow\b"
        + @"|\bDeckTranslationSettingsRow\s*\.\s*From\b"
        + @"|\b(DeckTranslationSettings|Set<DeckTranslationSettingsRow>\(\))[^;]*?\.\s*(Add|AddRange|AddAsync|AddRangeAsync|Attach|Update|UpdateRange|Remove|RemoveRange|ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\b"
        + @"|\b(INSERT\s+(OR\s+\w+\s+)?INTO|UPDATE|DELETE\s+FROM|REPLACE\s+INTO)\s+[\\""]*DeckTranslationSettings\b",
        RegexOptions.CultureInvariant);

    private static readonly string[] _mayName =
    [
        "Features/Translations/", "Features/Slides/SlideEdits.cs", "Features/Slides/SlideCommands.cs", "Migrations/", "Data/AppDbContext.cs",
    ];

    [Test]
    public void TranslationsJson_WhenAssigned_ShouldBeUnderAllowedFiles()
    {
        var root = ProductionSource.WebApiDirectory();
        var files = ProductionSource.Matching(["**/*.cs"])
            .Where(file => !_mayName.Any(allowed => Relative(root, file).StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        files.ShouldNotBeEmpty();
        files.Where(file => _translationWrite.IsMatch(File.ReadAllText(file))).Select(file => Relative(root, file)).ShouldBeEmpty();
    }

    // The guard's own pattern, so a typo in it can't turn the rule above into one that passes on anything.
    [TestCase("slide.TranslationsJson = json;")]
    [TestCase("var row = new DeckTranslationSettingsRow { DeckId = id, Primary = \"en\", LanguagesJson = \"[]\" };")]
    [TestCase("db.DeckTranslationSettings.Add(row);")]
    [TestCase("db.Set<DeckTranslationSettingsRow>().Remove(row);")]
    [TestCase("var row = DeckTranslationSettingsRow.From(id, settings);")]
    [TestCase("await db.Database.ExecuteSqlAsync($\"UPDATE \\\"DeckTranslationSettings\\\" SET Enabled = 1\");")]
    public void TranslationWritePattern_WhenLineWritesTranslations_ShouldMatch(string line)
    {
        _translationWrite.IsMatch(line).ShouldBeTrue(line);
    }

    [TestCase("var json = slide.TranslationsJson;")]
    [TestCase("if (slide.TranslationsJson == null) { return; }")]
    [TestCase("var row = await db.DeckTranslationSettings.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == id);")]
    public void TranslationWritePattern_WhenLineOnlyReads_ShouldNotMatch(string line)
    {
        _translationWrite.IsMatch(line).ShouldBeFalse(line);
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
}
