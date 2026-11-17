using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// SW-2's Measure (D6, D14): the SQL in docs/measures.md, read from the file as written and run through the app's own
// database connection. Decks go in through the public API with the clock moved between them, so the creation and look
// entries carry the texts the code really writes. The catalogue holds the built-ins plus a locking template that isn't
// built in, which must never count as adopted.
[TestFixture]
public partial class LookAdoptionQueryTests : ApiTestFixture
{
    private const string MEASURES = "docs/measures.md";
    private const string HEADING = "## Look adoption (SW-2)";

    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.AddSingleton<ITemplateCatalogue>(new LockingTemplateCatalogue());

    [Test]
    public async Task Query_CountsNewDecksInTheWindowOnABuiltInLookWithNoOverride()
    {
        await CreateDeckAsync("before the window");
        Clock.Advance(TimeSpan.FromDays(1));
        var from = Clock.GetUtcNow();

        await CreateDeckAsync("plain");
        var relooked = await CreateDeckAsync("relooked", template: "workshop", palette: "ember");
        await PutLookAsync(relooked, new { template = "update", palette = "plum" });
        var chrome = await CreateDeckAsync("chrome");
        await PutLookAsync(chrome, new { chrome = new { tracker = false } });
        var reset = await CreateDeckAsync("chrome reset");
        await PutLookAsync(reset, new { chrome = new { progress = false } });
        using var resetChrome = JsonDocument.Parse("""{ "chrome": { "progress": null } }""");
        await PutLookAsync(reset, resetChrome.RootElement);
        await CreateDeckWithALogoAsync();
        await CreateDeckAsync("locked", template: LockingTemplateCatalogue.LOCKING);
        Clock.Advance(TimeSpan.FromHours(1));
        var to = Clock.GetUtcNow();

        Clock.Advance(TimeSpan.FromDays(1));
        await CreateDeckAsync("after the window");

        var row = await RunAsync(from, to);

        row.ShouldBe(new Dictionary<string, object?>
        {
            ["new_decks"] = 6L,
            ["adopted"] = 3L,
            ["adopted_pct"] = 50.0,
            ["relooked"] = 1L,
        });
    }

    [Test]
    public async Task Query_WhenNoDeckInTheWindow_ShouldCountNothing()
    {
        await CreateDeckAsync("before the window");
        Clock.Advance(TimeSpan.FromDays(1));

        var row = await RunAsync(Clock.GetUtcNow(), Clock.GetUtcNow().AddDays(1));

        row.ShouldBe(new Dictionary<string, object?>
        {
            ["new_decks"] = 0L,
            ["adopted"] = 0L,
            ["adopted_pct"] = null,
            ["relooked"] = 0L,
        });
    }

    // The query lists the built-in names, so a template or palette added to the catalogue without the doc fails here.
    [Test]
    public void Query_ListsExactlyTheBuiltInTemplatesAndPalettes()
    {
        var sql = MeasureSql();

        NamesIn(sql, "d.Template").ShouldBe(new TemplateCatalogue().All.Select(t => t.Name), ignoreOrder: true);
        NamesIn(sql, "d.Palette").ShouldBe(new PaletteCatalogue().All.Select(p => p.Name), ignoreOrder: true);
    }

    private async Task<Dictionary<string, object?>> RunAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var scope = Factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = MeasureSql();
        AddParameter(command, "@from", from);
        AddParameter(command, "@to", to);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue("the Measure returns one row");
        var row = Enumerable.Range(0, reader.FieldCount)
            .ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i));
        (await reader.ReadAsync()).ShouldBeFalse("the Measure returns one row");
        return row;
    }

    private static void AddParameter(DbCommand command, string name, DateTimeOffset value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        command.Parameters.Add(parameter);
    }

    private async Task<string> CreateDeckAsync(string title, string? template = null, string? palette = null)
    {
        var response = await Client.PostAsJsonAsync("/api/decks", new { title, template, palette });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    // Made by an agent (create_deck), so the Measure counts both ways a deck is created.
    private async Task CreateDeckWithALogoAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title: "logo");
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        await PutLookAsync(deck, new { logo = image });
    }

    private async Task PutLookAsync<T>(string deck, T body)
    {
        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/look", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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

    private static List<string> NamesIn(string sql, string column)
    {
        var match = Regex.Match(sql, Regex.Escape(column) + @"\s+IN\s*\((?<names>[^)]*)\)");
        match.Success.ShouldBeTrue($"the Measure has no '{column} IN (...)' list");
        return [.. Regex.Matches(match.Groups["names"].Value, "'([^']*)'").Select(name => name.Groups[1].Value)];
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
