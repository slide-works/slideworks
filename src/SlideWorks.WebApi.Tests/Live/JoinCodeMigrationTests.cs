using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Tests.Live;

// Builds a database as SW-4 left it, with decks in it, then applies the SW-5 migration. No host starts: a migration
// can only be tested against rows that already exist, so these rows go in with SQL.
[TestFixture]
public class JoinCodeMigrationTests
{
    private const int DECK_COUNT = 40;

    private string _dbPath = null!;

    [SetUp]
    public void CreateDatabasePath() => _dbPath = Path.Combine(Path.GetTempPath(), $"sw-migration-{Guid.NewGuid():N}.db");

    [TearDown]
    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Test]
    public async Task Migration_WhenDecksExist_ShouldBackfillDistinctCodes()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options);
        var migrator = db.GetService<IMigrator>();
        var all = db.Database.GetMigrations().ToList();
        var sw5 = all.Single(m => m.EndsWith("_Sw5AudienceJoin", StringComparison.Ordinal));
        await migrator.MigrateAsync(all[all.IndexOf(sw5) - 1]);
        var owner = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email, Name, GoogleSubject, CreatedAt, Access, IsAdmin) VALUES ({0}, 'sam@example.com', 'Sam', 'sub-1', 0, 'Approved', 0)",
            owner.ToString().ToUpperInvariant());
        for (var i = 0; i < DECK_COUNT; i++)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Decks (Id, Slug, Title, PartsJson, OwnerId, Sharing, Revision, CreatedAt, UpdatedAt, Template, Palette, Draft) "
                + "VALUES ({0}, 'd', 'Deck', '[]', {1}, 'Private', 0, 0, 0, 'talk', 'cobalt', 0)",
                $"deck{i:D8}", owner.ToString().ToUpperInvariant());
        }

        await migrator.MigrateAsync();

        var codes = await db.Database.SqlQueryRaw<string>("SELECT JoinCode AS Value FROM Decks").ToListAsync();
        codes.Count.ShouldBe(DECK_COUNT);
        codes.Distinct().Count().ShouldBe(DECK_COUNT);
        codes.ShouldAllBe(c => c.Length == 6 && c.All(char.IsAsciiDigit) && int.Parse(c) >= 100000 && int.Parse(c) <= 999999);
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }
}
