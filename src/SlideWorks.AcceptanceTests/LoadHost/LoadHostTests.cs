using System.Diagnostics;

namespace SlideWorks.LoadHost;

// Not a test: the supported way to start a seeded app for a load run by hand (docs/load.md, D17). Explicit and in the LoadHost
// category, so no suite runs it; select it by name (docs/load.md). It starts LoadHostApp on LOADHOST_PORT (default 5510), seeds a live deck with a results-live
// poll (LoadSeed), writes the join code, deck id, poll slide and owner cookie to LOADHOST_DIR/seed.env and to the test's
// progress output, and serves until LOADHOST_DIR/stop exists or LOADHOST_MINUTES (default 60) pass. The database and data
// folder go when it stops.
[Explicit("Starts a seeded app for a load run by hand: docs/load.md")]
[Category(CATEGORY)]
public class LoadHostTests
{
    // Also excluded by category, so a run filtered on TestCategory!=LoadHost leaves it out even if a filter names it.
    public const string CATEGORY = "LoadHost";

    [Test]
    public async Task Serve_WhenStarted_ShouldServeALiveDeckUntilStopped()
    {
        var port = int.Parse(Environment.GetEnvironmentVariable("LOADHOST_PORT") ?? "5510");
        var folder = Environment.GetEnvironmentVariable("LOADHOST_DIR") ?? Path.Combine(Path.GetTempPath(), "slide-works-load-host");
        var minutes = int.Parse(Environment.GetEnvironmentVariable("LOADHOST_MINUTES") ?? "60");
        Directory.CreateDirectory(folder);
        var stop = Path.Combine(folder, "stop");
        File.Delete(stop);

        using var app = new LoadHostApp(port);
        var seed = await LoadSeed.CreateAsync(app);
        var env = $"""
            URL={app.BaseUrl}
            CODE={seed.Code}
            DECK={seed.DeckId}
            SLIDE={LoadSeed.POLL_SLIDE}
            OWNER_COOKIE={LoadSeed.OwnerCookie}
            HUB_URLS={string.Join(",", Enumerable.Range(1, LoadHostApp.ADDRESSES).Select(last => $"http://127.0.0.{last}:{port}"))}

            """;
        await File.WriteAllTextAsync(Path.Combine(folder, "seed.env"), env);
        await TestContext.Progress.WriteLineAsync(env);

        var started = Stopwatch.GetTimestamp();
        using var tick = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!File.Exists(stop) && Stopwatch.GetElapsedTime(started) < TimeSpan.FromMinutes(minutes))
        {
            await tick.WaitForNextTickAsync();
        }
        File.Delete(stop);

        seed.Code.ShouldNotBeNullOrEmpty();
    }
}
