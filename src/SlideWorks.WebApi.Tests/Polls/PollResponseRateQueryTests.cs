using System.Data.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// SW-6's Measure (V10): the SQL in docs/measures.md, read from the file as written and run through the app's own database
// connection. Rounds and answers are seeded through PollRowSeed, closed the way closing writes them. A draft is discarded by
// ending the live session, which is how the app discards one.
[TestFixture]
public partial class PollResponseRateQueryTests : PollTestFixture
{
    private const string MEASURES = "docs/measures.md";
    private const string HEADING = "## Poll response rate (SW-6)";

    [Test]
    public async Task PollResponseRate_WhenTwoRoundsClosed_ShouldReturnOneRowEach()
    {
        var poll = await SeedPollAsync();
        var from = Clock.GetUtcNow();
        await CloseRoundAsync(poll, number: 1, joined: 8, answers: 6);
        Clock.Advance(TimeSpan.FromMinutes(5));
        await CloseRoundAsync(poll, number: 2, joined: 10, answers: 4);
        Clock.Advance(TimeSpan.FromMinutes(5));

        var rows = await RunAsync(from, Clock.GetUtcNow());

        rows.Count.ShouldBe(2);
        rows[0].ShouldBe(Row(poll, number: 2, answered: 4, joined: 10, rate: 0.4));
        rows[1].ShouldBe(Row(poll, number: 1, answered: 6, joined: 8, rate: 0.75));
    }

    [Test]
    public async Task PollResponseRate_WhenMoreAnsweredThanJoined_ShouldCapTheRateAtOneAndKeepTheCounts()
    {
        var poll = await SeedPollAsync();
        var from = Clock.GetUtcNow();
        await CloseRoundAsync(poll, number: 1, joined: 2, answers: 5);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var rows = await RunAsync(from, Clock.GetUtcNow());

        rows.ShouldBe([Row(poll, number: 1, answered: 5, joined: 2, rate: 1.0)]);
    }

    [Test]
    public async Task PollResponseRate_WhenNobodyHadJoined_ShouldReturnNoRateAndNotFail()
    {
        var poll = await SeedPollAsync();
        var from = Clock.GetUtcNow();
        await CloseRoundAsync(poll, number: 1, joined: 0, answers: 0);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var rows = await RunAsync(from, Clock.GetUtcNow());

        rows.ShouldBe([Row(poll, number: 1, answered: 0, joined: 0, rate: null)]);
    }

    [Test]
    public async Task PollResponseRate_WhenARoundIsOpenOrWasADiscardedDraft_ShouldLeaveItOut()
    {
        var poll = await SeedPollAsync();
        var from = Clock.GetUtcNow();
        await CloseRoundAsync(poll, number: 1, joined: 4, answers: 2);
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);
        Clock.Advance(TimeSpan.FromMinutes(1));
        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();
        var next = await GoLiveAsync(poll.DeckId);
        await Rows.RoundAsync(poll.SlideId, next, PollPhase.Open, number: 1);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var rows = await RunAsync(from, Clock.GetUtcNow());

        rows.ShouldBe([Row(poll, number: 1, answered: 2, joined: 4, rate: 0.5)]);
    }

    [Test]
    public async Task PollResponseRate_WhenARoundClosedOutsideTheWindow_ShouldLeaveItOut()
    {
        var poll = await SeedPollAsync();
        await CloseRoundAsync(poll, number: 1, joined: 4, answers: 4);
        Clock.Advance(TimeSpan.FromDays(1));
        var from = Clock.GetUtcNow();
        await CloseRoundAsync(poll, number: 2, joined: 5, answers: 1);
        Clock.Advance(TimeSpan.FromHours(1));
        var to = Clock.GetUtcNow();
        Clock.Advance(TimeSpan.FromDays(1));
        await CloseRoundAsync(poll, number: 3, joined: 5, answers: 5);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var rows = await RunAsync(from, to);

        rows.ShouldBe([Row(poll, number: 2, answered: 1, joined: 5, rate: 0.2)]);
    }

    private async Task CloseRoundAsync(SeededPoll poll, int number, int joined, int answers)
    {
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Open, number);
        for (var guest = 0; guest < answers; guest++)
        {
            await Rows.AnswerAsync(round.Id, $"guest-{number}-{guest}", guest % 3);
        }
        await Rows.CloseAsync(round.Id, joined);
    }

    private async Task<Guid> GoLiveAsync(string deckId)
    {
        var response = await Client.PostAsync($"/api/decks/{deckId}/live", null);
        response.EnsureSuccessStatusCode();
        var live = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(live.GetProperty("sessionId").GetString()!);
    }

    private static Dictionary<string, object?> Row(SeededPoll poll, int number, long answered, long joined, double? rate) => new()
    {
        ["session_id"] = poll.SessionId.ToString().ToUpperInvariant(),
        ["deck_id"] = poll.DeckId,
        ["slide_id"] = poll.SlideId.ToString().ToUpperInvariant(),
        ["round_number"] = (long)number,
        ["answered"] = answered,
        ["joined"] = joined,
        ["rate"] = rate,
    };

    private async Task<List<Dictionary<string, object?>>> RunAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var scope = Factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MeasureSql();
        AddParameter(command, "@from", from);
        AddParameter(command, "@to", to);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i));
            row["closed_at"].ShouldNotBeNull("a closed round has a close time");
            row.Remove("closed_at");
            rows.Add(row);
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

    private static string MeasureSql()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), MEASURES));
        var section = text.IndexOf(HEADING, StringComparison.Ordinal);
        section.ShouldBeGreaterThanOrEqualTo(0, $"{MEASURES} has no '{HEADING}' section");
        var match = SqlBlock().Match(text, section);
        match.Success.ShouldBeTrue($"'{HEADING}' in {MEASURES} has no ```sql block");
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

    [GeneratedRegex(@"```sql\n(?<sql>.*?)```", RegexOptions.Singleline)]
    private static partial Regex SqlBlock();
}
