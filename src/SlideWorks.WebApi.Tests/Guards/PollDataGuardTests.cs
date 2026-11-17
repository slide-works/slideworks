using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Tests.Guards;

// Rounds, answers and options are written only by the poll feature and SW-9's Q&A feature (V4, V5; SW-9 Q2): opening,
// closing and changing a slide round go through IPolls, answers through the batching writer, Q&A rounds and their options
// through Features/Qa. A line elsewhere that makes, adds, updates or deletes one, through EF or raw SQL, fails here by
// naming the file. The model configuration and the migrations are the only other files that name the tables.
[TestFixture]
public class PollDataGuardTests
{
    private static readonly Regex _pollWrite = new(
        @"new\s+Poll(Round|Answer|Option)\b"
        + @"|\b(PollRounds|PollAnswers|PollOptions|Set<Poll(Round|Answer|Option)>\(\))[^;]*?\.\s*(Add|AddRange|AddAsync|AddRangeAsync|Attach|Update|UpdateRange|Remove|RemoveRange|ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\b"
        + @"|\b(INSERT\s+(OR\s+\w+\s+)?INTO|UPDATE|DELETE\s+FROM|REPLACE\s+INTO)\s+[\\""]*Poll(Rounds|Answers|Options)\b",
        RegexOptions.CultureInvariant);

    private static readonly string[] _mayName = ["Features/Polls/", "Features/Qa/", "Migrations/", "Data/AppDbContext.cs"];

    [Test]
    public void PollRoundsAndAnswers_ShouldBeWrittenOnlyInFeaturesPolls()
    {
        var root = ProductionSource.WebApiDirectory();
        var files = ProductionSource.Matching(["**/*.cs"])
            .Where(file => !_mayName.Any(allowed => Relative(root, file).StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        files.ShouldNotBeEmpty();
        files.Where(file => _pollWrite.IsMatch(File.ReadAllText(file))).Select(file => Relative(root, file)).ShouldBeEmpty();
    }

    // The guard's own pattern, so a typo in it can't turn the rule above into one that passes on anything.
    [TestCase("var round = new PollRound { Id = id };")]
    [TestCase("db.PollAnswers.Add(answer);")]
    [TestCase("await db.PollRounds .Where(r => r.Id == id).ExecuteUpdateAsync(set => set.SetProperty(r => r.ClosedAt, now));")]
    [TestCase("db.Set<PollAnswer>().Remove(answer);")]
    [TestCase("await db.Database.ExecuteSqlAsync($\"UPDATE \\\"PollRounds\\\" SET ClosedAt = {now}\");")]
    [TestCase("INSERT OR REPLACE INTO PollAnswers (RoundId) VALUES (@id)")]
    [TestCase("var option = new PollOption { RoundId = id, Position = 0, Text = text };")]
    [TestCase("db.PollOptions.Add(option);")]
    [TestCase("INSERT INTO \"PollOptions\" (RoundId, Position) VALUES (@id, 0)")]
    public void PollWritePattern_WhenLineWritesARoundOrAnswer_ShouldMatch(string line)
    {
        _pollWrite.IsMatch(line).ShouldBeTrue(line);
    }

    [TestCase("var open = await db.PollRounds.SingleOrDefaultAsync(r => r.SessionId == id);")]
    [TestCase("var mine = await db.PollAnswers.AsNoTracking().Where(a => a.GuestId == guest).ToListAsync();")]
    [TestCase("var texts = await db.PollOptions.AsNoTracking().Where(o => o.RoundId == id).ToListAsync();")]
    public void PollWritePattern_WhenLineOnlyReads_ShouldNotMatch(string line)
    {
        _pollWrite.IsMatch(line).ShouldBeFalse(line);
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
}
