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

    [Test]
    public async Task Startup_WhenFreshDatabase_ShouldApplyEveryMigration()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        // SW-54 adds a third migration after these two (S1), SW-2 a fourth (L5) and SW-4 a fifth (P7), SW-5 a sixth (the join code, sessions and joins),
        // SW-6 a seventh (polls, rounds and answers), SW-9 an eighth (the poll tables made general) and a ninth (its questions, settings, switches and reactions),
        // SW-50 a tenth (templates, a deck's template version and a slide's surface).
        applied.Count.ShouldBe(10);
        applied[0].ShouldEndWith("_Initial");
        applied[1].ShouldEndWith("_Sw1Foundation");
        applied[2].ShouldEndWith("_Sw54SignUpApproval");
        applied[3].ShouldEndWith("_Sw2Look");
        applied[4].ShouldEndWith("_Sw4TalkLength");
        applied[5].ShouldEndWith("_Sw5AudienceJoin");
        applied[6].ShouldEndWith("_Sw6Polls");
        applied[7].ShouldEndWith("_Sw9PollKinds");
        applied[8].ShouldEndWith("_Sw9Qa");
        applied[9].ShouldEndWith("_Sw50Templates");
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
