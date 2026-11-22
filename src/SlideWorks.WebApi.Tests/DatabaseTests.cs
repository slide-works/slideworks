using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class DatabaseTests : ApiTestFixture
{
    private static readonly string[] SW1_TABLES =
    [
        "Users", "Decks", "Slides", "ActivityEntries", "DeckViewers", "DeckImages",
        "OAuthClients", "OAuthRequests", "OAuthCodes", "AgentTokens", "AudienceSessions", "AudienceJoins",
    ];

    [Test]
    public async Task Startup_WhenAppBoots_AppliesMigrationsToTheConfiguredSqliteFile()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    // A new migration is added to the end of KNOWN_MIGRATIONS, the one list to update.
    private static readonly string[] KNOWN_MIGRATIONS =
    [
        "_Initial", "_Sw1Foundation", "_Sw54SignUpApproval", "_Sw2Look", "_Sw4TalkLength", "_Sw5AudienceJoin", "_Sw6Polls", "_Sw9PollKinds",
        "_Sw9Qa", "_Sw50Templates", "_Sw556PaletteTokens", "_Sw568OpenAnswers", "_Sw8OpenResponses", "_Sw8OpenResponseSlideKey",
    ];

    [Test]
    public async Task Startup_WhenFreshDatabase_ShouldApplyEveryMigration()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        var message = "Applied migrations differ from KNOWN_MIGRATIONS in DatabaseTests.cs: add the new migration's name suffix to the end of that list. "
            + $"Applied: [{string.Join(", ", applied)}].";
        applied.Count.ShouldBe(KNOWN_MIGRATIONS.Length, message);
        for (var i = 0; i < KNOWN_MIGRATIONS.Length; i++)
        {
            applied[i].EndsWith(KNOWN_MIGRATIONS[i], StringComparison.Ordinal).ShouldBeTrue(message);
        }
    }

    [Test]
    public async Task Startup_WhenFreshDatabase_ShouldCreateEverySw1Table()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();

        foreach (var table in SW1_TABLES)
        {
            tables.ShouldContain(table);
        }
        tables.ShouldNotContain("Notes");
    }
}
