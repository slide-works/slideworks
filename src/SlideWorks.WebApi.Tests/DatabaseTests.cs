using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Tests;

[TestFixture]
public class DatabaseTests : ApiTestFixture
{
    [Test]
    public async Task Startup_WhenAppBoots_AppliesMigrationsToTheConfiguredSqliteFile()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }
}
