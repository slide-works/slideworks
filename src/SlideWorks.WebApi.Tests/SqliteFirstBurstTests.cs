using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Tests;

// SW-164: the first concurrent burst on a fresh host sometimes got a 500, "SQLite Error 5: unable to delete/modify
// collation sequence due to active statements", from SqliteConnection.Open. Microsoft.Data.Sqlite before 10.0.13 could
// hand one pooled sqlite3 handle to two openers while the pool grew (dotnet/efcore#39008): a connection being activated
// looked leaked for an instant, a second opener reclaimed it, and EF's EF_DECIMAL collation was re-registered on it while
// the first opener's statement ran. The pool only grows on a first burst, so each round empties this host's pool first
// and then fires the burst, as a freshly started host would see it.
[TestFixture]
public class SqliteFirstBurstTests : ApiTestFixture
{
    private const int ROUNDS = 150;
    private const int WIDTH = 32;

    [Test]
    public async Task Burst_WhenConnectionPoolIsFresh_ShouldServeEveryRequest()
    {
        var failures = new List<string>();

        for (var round = 0; round < ROUNDS && failures.Count == 0; round++)
        {
            EmptyConnectionPool();
            using var start = new Barrier(WIDTH);
            var responses = await Task.WhenAll(Enumerable.Range(0, WIDTH).Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(TimeSpan.FromSeconds(30)).ShouldBeTrue("every request should reach the start line");
                    return Client.GetAsync("/api/decks");
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap()));

            foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
            {
                failures.Add($"round {round}: {(int)refused.StatusCode} {(await refused.Content.ReadAsStringAsync()).Split('\n')[0]}");
            }
        }

        failures.ShouldBeEmpty();
    }

    private void EmptyConnectionPool()
    {
        using var scope = Factory.Services.CreateScope();
        var connection = (SqliteConnection)scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();
        SqliteConnection.ClearPool(connection);
    }
}
