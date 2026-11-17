using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Tests.Guards;

// SW-9's rows (Q2, Q6, Q7) are written only by the Q&A feature: questions by asking and moderation, the settings and
// switch rows by the settings service, reaction totals and guests by the reactions service. A line elsewhere that makes,
// adds, updates or deletes one, through EF or raw SQL, fails here by naming the file. The model configuration and the
// migrations are the only other files that name the tables.
[TestFixture]
public class QaDataGuardTests
{
    private const string ROWS = "Question|QaSettingsRow|QaSlideSwitchRow|ReactionTotal|ReactionGuest";

    private const string TABLES = "Questions|QaSettingsRows|QaSlideSwitchRows|ReactionTotals|ReactionGuests";

    private static readonly Regex _qaWrite = new(
        $@"new\s+({ROWS})\b"
        + @"|\b(QaSettingsRow|QaSlideSwitchRow)\s*\.\s*From\b"
        + $@"|\b({TABLES}|Set<({ROWS})>\(\))[^;]*?\.\s*(Add|AddRange|AddAsync|AddRangeAsync|Attach|Update|UpdateRange|Remove|RemoveRange|ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\b"
        + $@"|\b(INSERT\s+(OR\s+\w+\s+)?INTO|UPDATE|DELETE\s+FROM|REPLACE\s+INTO)\s+[\\""]*({TABLES})\b",
        RegexOptions.CultureInvariant);

    private static readonly string[] _mayName = ["Features/Qa/", "Migrations/", "Data/AppDbContext.cs"];

    [Test]
    public void QaRows_ShouldBeWrittenOnlyInFeaturesQa()
    {
        var root = ProductionSource.WebApiDirectory();
        var files = ProductionSource.Matching(["**/*.cs"])
            .Where(file => !_mayName.Any(allowed => Relative(root, file).StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        files.ShouldNotBeEmpty();
        files.Where(file => _qaWrite.IsMatch(File.ReadAllText(file))).Select(file => Relative(root, file)).ShouldBeEmpty();
    }

    // The guard's own pattern, so a typo in it can't turn the rule above into one that passes on anything.
    [TestCase("var question = new Question { RoundId = id, Position = 0, GuestId = guest };")]
    [TestCase("db.Questions.Add(question);")]
    [TestCase("await db.Questions .Where(q => q.RoundId == id).ExecuteUpdateAsync(set => set.SetProperty(q => q.OnScreen, false));")]
    [TestCase("db.Set<ReactionTotal>().Update(total);")]
    [TestCase("db.ReactionGuests.Add(new ReactionGuest { SessionId = id, GuestId = guest });")]
    [TestCase("db.QaSettingsRows.Add(QaSettingsRow.From(deckId, settings));")]
    [TestCase("var row = QaSlideSwitchRow.From(slideId, slideSwitch);")]
    [TestCase("await db.Database.ExecuteSqlAsync($\"UPDATE \\\"Questions\\\" SET HiddenAt = {now}\");")]
    [TestCase("INSERT OR REPLACE INTO ReactionTotals (SessionId) VALUES (@id)")]
    [TestCase("DELETE FROM \"QaSlideSwitchRows\" WHERE SlideId = @id")]
    public void QaWritePattern_WhenLineWritesAQaRow_ShouldMatch(string line)
    {
        _qaWrite.IsMatch(line).ShouldBeTrue(line);
    }

    [TestCase("var questions = await db.Questions.AsNoTracking().Where(q => q.RoundId == id).ToListAsync();")]
    [TestCase("var settings = (await db.QaSettingsRows.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId))?.ToSettings();")]
    [TestCase("var view = new QuestionTally(deckId, round, revision, votes);")]
    [TestCase("var question = new GuestQuestion(1, text, null, \"now\", QuestionStatus.Open, 3, false, true);")]
    [TestCase("var rules = QuestionText.Check(text, name, true);")]
    public void QaWritePattern_WhenLineOnlyReadsOrNamesAnotherType_ShouldNotMatch(string line)
    {
        _qaWrite.IsMatch(line).ShouldBeFalse(line);
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
}
