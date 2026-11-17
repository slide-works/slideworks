using System.Data.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Live;

// SW-5's Measures (D13, J8): the two queries in docs/measures.md, read from the file as written and run through the app's
// own database connection. The relay is driven the way the relay tests do, with a settable registry standing in for SW-4's,
// so the join slide's first showing is stamped by the real code, and guests come in over the real hub. The hub writes a
// guest's join row before JoinAudience answers, so a query run after the joins sees them.
[TestFixture]
[CancelAfter(60000)]
public partial class MeasuresSqlTests : ApiTestFixture
{
    private const string MEASURES = "docs/measures.md";
    private const string SECTION = "## Audience join (SW-5)";
    private const string PEAK_HEADING = "### Peak joined per session";
    private const string SPEED_HEADING = "### Seconds from the join slide to each join";

    private readonly List<GuestClient> _guests = [];

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var guest in _guests)
        {
            await guest.DisposeAsync();
        }
        _guests.Clear();
    }

    [Test]
    public async Task MeasuresSql_WhenTwoGuestsJoin_ShouldReportPeakTwo()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (deck, code, session) = await GoLiveOnAJoinSlideAsync();
        await JoinAsync(code);
        await JoinAsync(code);
        Clock.Advance(TimeSpan.FromHours(1));

        var rows = await RunAsync(PEAK_HEADING, from, Clock.GetUtcNow());

        var row = rows.Single(r => IsSession(r, session));
        row["deck_id"].ShouldBe(deck);
        row["peak_joined"].ShouldBe(2L);
    }

    // Three guests join in all, but the first has left before the third comes, so never more than two were there at once.
    [Test]
    public async Task MeasuresSql_WhenAGuestLeavesBeforeAnotherJoins_ShouldReportPeakTwoOfThreeJoins()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (deck, code, session) = await GoLiveOnAJoinSlideAsync();
        var counts = Channel.CreateUnbounded<int>();
        using var subscription = Factory.Services.GetRequiredService<IAudienceEvents>()
            .Subscribe(deck, e => e is JoinedCount count ? Task.FromResult(counts.Writer.TryWrite(count.Count)) : Task.CompletedTask);
        var first = await JoinAsync(code);
        await JoinAsync(code);
        await NextCountAsync(counts);
        await NextCountAsync(counts);
        await first.DisposeAsync();
        // The roster publishes a count for each leave, so reading it proves the leave was processed before the next join.
        (await NextCountAsync(counts)).ShouldBe(1);
        await JoinAsync(code);

        var peak = (await RunAsync(PEAK_HEADING, from, Clock.GetUtcNow().AddMinutes(1))).Single(r => IsSession(r, session));
        var speed = (await RunAsync(SPEED_HEADING, from, Clock.GetUtcNow().AddMinutes(1))).Single(r => IsSession(r, session));

        peak["peak_joined"].ShouldBe(2L);
        speed["joins"].ShouldBe(3L);
    }

    [Test]
    public async Task MeasuresSql_WhenGuestsJoinAfterTheJoinSlide_ShouldReportTheMedianSeconds()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (_, code, session) = await GoLiveOnAJoinSlideAsync();
        Clock.Advance(TimeSpan.FromSeconds(10));
        await JoinAsync(code);
        Clock.Advance(TimeSpan.FromSeconds(20));
        await JoinAsync(code);
        Clock.Advance(TimeSpan.FromSeconds(60));
        await JoinAsync(code);

        var rows = await RunAsync(SPEED_HEADING, from, Clock.GetUtcNow().AddMinutes(1));

        var row = rows.Single(r => IsSession(r, session));
        row["joins"].ShouldBe(3L);
        row["before_slide"].ShouldBe(0L);
        row["median_seconds"].ShouldBe(30.0);
    }

    // With an even count the median is the mean of the two middle gaps: 10 s and 30 s give 20 s.
    [Test]
    public async Task MeasuresSql_WhenAnEvenNumberOfGuestsJoin_ShouldReportTheMeanOfTheMiddleTwo()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (_, code, session) = await GoLiveOnAJoinSlideAsync();
        Clock.Advance(TimeSpan.FromSeconds(10));
        await JoinAsync(code);
        Clock.Advance(TimeSpan.FromSeconds(20));
        await JoinAsync(code);

        var rows = await RunAsync(SPEED_HEADING, from, Clock.GetUtcNow().AddMinutes(1));

        var row = rows.Single(r => IsSession(r, session));
        row["joins"].ShouldBe(2L);
        row["median_seconds"].ShouldBe(20.0);
    }

    // The guest was counted when Go live ran, which is before the join slide was first shown, so JoinedAt is earlier than the
    // stamp. Unclamped, the median would be minus 300 seconds.
    [Test]
    public async Task MeasuresSql_WhenGuestJoinedInLobby_ShouldNotGoNegative()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (deck, code) = await SeedAsync();
        await JoinAsync(code);
        var session = await PostLiveAsync(deck);
        Clock.Advance(TimeSpan.FromMinutes(5));
        await MoveAsync(deck, slide: 2);

        var rows = await RunAsync(SPEED_HEADING, from, Clock.GetUtcNow().AddMinutes(1));

        var row = rows.Single(r => IsSession(r, session));
        row["joins"].ShouldBe(1L);
        row["before_slide"].ShouldBe(1L);
        row["median_seconds"].ShouldBe(0.0);
    }

    [Test]
    public async Task MeasuresSql_WhenJoinSlideWasNeverShown_ShouldHaveNoSpeedRow()
    {
        var from = Clock.GetUtcNow().AddMinutes(-1);
        var (deck, code) = await SeedAsync();
        var session = await PostLiveAsync(deck);
        await JoinAsync(code);

        var rows = await RunAsync(SPEED_HEADING, from, Clock.GetUtcNow().AddMinutes(1));

        rows.ShouldNotContain(r => IsSession(r, session));
    }

    // A deck with a quote slide and then a join slide; no session is open yet, so its code opens a lobby.
    private async Task<(string Deck, string Code)> SeedAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Before the join slide");
        await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
        var code = (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/live")).GetProperty("code").GetString()!;
        return (deck, code);
    }

    private async Task<(string Deck, string Code, string Session)> GoLiveOnAJoinSlideAsync()
    {
        var (deck, code) = await SeedAsync();
        var session = await PostLiveAsync(deck);
        await MoveAsync(deck, slide: 2);
        return (deck, code, session);
    }

    private static async Task<int> NextCountAsync(Channel<int> counts)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await counts.Reader.ReadAsync(timeout.Token);
    }

    // The fixture shares one app, so earlier tests' sessions are in the window too: each test looks for its own.
    private static bool IsSession(Dictionary<string, object?> row, string session)
        => Guid.Parse((string)row["session_id"]!) == Guid.Parse(session);

    private async Task<string> PostLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
    }

    private async Task<GuestClient> JoinAsync(string code)
    {
        var guest = await GuestClient.ConnectAsync(Factory);
        _guests.Add(guest);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        return guest;
    }

    // What SW-4's registry does on an accepted change: record the state, then tell every observer.
    private async Task MoveAsync(string deck, int slide)
    {
        var state = SettablePresentations.At(deck, slide);
        Factory.Services.GetRequiredService<SettablePresentations>().Set(state);
        foreach (var observer in Factory.Services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
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
