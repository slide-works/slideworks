using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// SW-9's Measures (D9): the two queries in docs/measures.md, read from the file as written and run through the app's own
// database connection. Go live opens the session's Q&A round; questions, upvotes, reactors and joins are written straight to
// the database, so each test says exactly who did what.
[TestFixture]
public partial class QaMeasuresSqlTests : PollTestFixture
{
    private const string MEASURES = "docs/measures.md";
    private const string SECTION = "## Q&A and reactions (SW-9)";
    private const string ASKED_HEADING = "### Questions asked per session";
    private const string SHARE_HEADING = "### Share of joined guests who asked, upvoted or reacted";

    private QaRowSeed Qa => new(Factory.Services);

    [Test]
    public async Task Asked_WhenSessionHasThreeQuestions_ShouldBeThree()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var poll = await SeedPollAsync();
        var round = await QaRoundAsync(poll.SessionId);
        await Qa.QuestionAsync(round, "First?", guestId: "guest-a");
        await Qa.QuestionAsync(round, "Second?", guestId: "guest-b", hidden: true);
        await Qa.QuestionAsync(round, "Third?", guestId: "guest-b", state: SlideWorks.Core.Qa.QuestionState.Answered);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var rows = await RunAsync(ASKED_HEADING, from, Clock.GetUtcNow().AddMinutes(1));

        var row = rows.Single(r => r["session_id"]!.ToString() == Upper(poll.SessionId));
        row["deck_id"].ShouldBe(poll.DeckId);
        row["asked"].ShouldBe(3L);
    }

    [Test]
    public async Task Share_WhenOneGuestAskedOneUpvotedOneReactedAndOneDidNothing_ShouldBeThreeQuarters()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var poll = await SeedPollAsync();
        var round = await QaRoundAsync(poll.SessionId);
        await JoinAsync(poll.SessionId, "asker", "upvoter", "reactor", "lurker");
        await Qa.QuestionAsync(round, "Why?", guestId: "asker");
        await Qa.VoteAsync(round, "upvoter", 1);
        await Qa.ReactionGuestAsync(poll.SessionId, "reactor");
        Clock.Advance(TimeSpan.FromMinutes(1));

        var row = await ShareRowAsync(poll, from);

        row["joined"].ShouldBe(4L);
        row["asked"].ShouldBe(1L);
        row["upvoted"].ShouldBe(1L);
        row["reacted"].ShouldBe(1L);
        row["took_part"].ShouldBe(3L);
        row["share"].ShouldBe(0.75);
    }

    [Test]
    public async Task Share_WhenAGuestDidAllThree_ShouldCountOnce()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var poll = await SeedPollAsync();
        var round = await QaRoundAsync(poll.SessionId);
        await JoinAsync(poll.SessionId, "keen", "lurker");
        await Qa.QuestionAsync(round, "One?", guestId: "keen");
        await Qa.QuestionAsync(round, "Two?", guestId: "keen");
        await Qa.VoteAsync(round, "keen", 1, 2);
        await Qa.ReactionGuestAsync(poll.SessionId, "keen");
        Clock.Advance(TimeSpan.FromMinutes(1));

        var row = await ShareRowAsync(poll, from);

        row["joined"].ShouldBe(2L);
        row["took_part"].ShouldBe(1L);
        row["share"].ShouldBe(0.5);
    }

    // A guest who never joined isn't in the room, so what they did doesn't count and doesn't raise the share.
    [Test]
    public async Task Share_WhenAGuestWhoNeverJoinedAsked_ShouldNotCountThem()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var poll = await SeedPollAsync();
        var round = await QaRoundAsync(poll.SessionId);
        await JoinAsync(poll.SessionId, "lurker");
        await Qa.QuestionAsync(round, "Who?", guestId: "stranger");
        Clock.Advance(TimeSpan.FromMinutes(1));

        var row = await ShareRowAsync(poll, from);

        row["took_part"].ShouldBe(0L);
        row["share"].ShouldBe(0.0);
    }

    // An upvote taken back leaves an answer with no picks, which isn't an upvote.
    [Test]
    public async Task Share_WhenAGuestTookBackEveryUpvote_ShouldNotCountThem()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var poll = await SeedPollAsync();
        var round = await QaRoundAsync(poll.SessionId);
        await JoinAsync(poll.SessionId, "waverer");
        await Qa.QuestionAsync(round, "Q?", guestId: "someone-else");
        await Qa.VoteAsync(round, "waverer", 1);
        await Qa.VoteAsync(round, "waverer");
        Clock.Advance(TimeSpan.FromMinutes(1));

        var row = await ShareRowAsync(poll, from);

        row["upvoted"].ShouldBe(0L);
        row["share"].ShouldBe(0.0);
    }

    private async Task<Dictionary<string, object?>> ShareRowAsync(SeededPoll poll, DateTimeOffset from)
    {
        var rows = await RunAsync(SHARE_HEADING, from, Clock.GetUtcNow().AddMinutes(1));
        return rows.Single(r => r["session_id"]!.ToString() == Upper(poll.SessionId));
    }

    private static string Upper(Guid id) => id.ToString().ToUpperInvariant();

    // The session's Q&A round, which Go live opened.
    private async Task<Guid> QaRoundAsync(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var round = await scope.ServiceProvider.GetRequiredService<PollRows>().OpenQaRoundAsync(sessionId);
        round.ShouldNotBeNull("Go live opens the session's Q&A round");
        return round.Id;
    }

    private async Task JoinAsync(Guid sessionId, params string[] guests)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AudienceJoins.AddRange(guests.Select(guest => new AudienceJoin { SessionId = sessionId, GuestId = guest, JoinedAt = Clock.GetUtcNow() }));
        await db.SaveChangesAsync();
    }

    private async Task<List<Dictionary<string, object?>>> RunAsync(string heading, DateTimeOffset from, DateTimeOffset to)
    {
        using var scope = Factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MeasureSql(heading);
        AddParameter(command, "@from", from);
        AddParameter(command, "@to", to);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            rows.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i)));
        }
        return rows;
    }

    private static void AddParameter(DbCommand command, string name, DateTimeOffset value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        command.Parameters.Add(parameter);
    }

    private static string MeasureSql(string heading)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), MEASURES));
        var section = text.IndexOf(SECTION, StringComparison.Ordinal);
        section.ShouldBeGreaterThanOrEqualTo(0, $"{MEASURES} has no '{SECTION}' section");
        var at = text.IndexOf(heading, section, StringComparison.Ordinal);
        at.ShouldBeGreaterThanOrEqualTo(0, $"'{SECTION}' in {MEASURES} has no '{heading}' heading");
        var match = SqlBlock().Match(text, at);
        match.Success.ShouldBeTrue($"'{heading}' in {MEASURES} has no ```sql block");
        return match.Groups["sql"].Value;
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, MEASURES)))
            {
                return dir.FullName;
            }
        }
        throw new FileNotFoundException($"No {MEASURES} above {TestContext.CurrentContext.TestDirectory}");
    }

    [GeneratedRegex(@"```sql\r?\n(?<sql>.*?)```", RegexOptions.Singleline)]
    private static partial Regex SqlBlock();
}
