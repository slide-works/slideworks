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
        "OAuthClients", "OAuthRequests", "OAuthCodes", "AgentTokens",
    ];

    [Test]
    public async Task Startup_WhenAppBoots_AppliesMigrationsToTheConfiguredSqliteFile()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Startup_WhenFreshDatabase_ShouldApplyBothMigrations()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        applied.Count.ShouldBe(2);
        applied[0].ShouldEndWith("_Initial");
        applied[1].ShouldEndWith("_Sw1Foundation");
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
