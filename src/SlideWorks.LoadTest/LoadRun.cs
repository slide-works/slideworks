using System.Diagnostics;

namespace SlideWorks.LoadTest;

// One run of a scenario: let it check the app is ready, join every guest (at most MAX_JOINS_IN_FLIGHT at a time, which keeps the
// app's join limiter under its in-flight cap), then give each guest a random moment inside the window to act, and wait
// for the results. What a guest does and what the report holds belong to the scenario (LoadScenario.cs).
public static class LoadRun
{
    public static async Task<IRunReport> RunAsync(LoadOptions options, TextWriter progress, CancellationToken ct)
    {
        using var http = new HttpClient(new SocketsHttpHandler { UseCookies = false, MaxConnectionsPerServer = int.MaxValue })
        {
            BaseAddress = options.Url,
            Timeout = TimeSpan.FromSeconds(30),
        };
        await using var presenter = options.OwnerCookie == null ? null : new Presenter(options, http);
        if (presenter != null)
        {
            await presenter.StartAsync(ct);
        }
        var scenario = LoadScenario.Create(options, http);
        await scenario.PrepareAsync(ct);
        var voters = Enumerable.Range(0, options.Voters).Select(i => scenario.CreateGuest(options.HubUrls[i % options.HubUrls.Count])).ToList();
        var phase = "1 before";
        using var stopProbes = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var probes = presenter?.ProbeAsync(() => phase, stopProbes.Token) ?? Task.CompletedTask;
        try
        {
            if (presenter != null)
            {
                // A few probes with nobody joined, for comparison.
                await Task.Delay(Presenter.PROBE_INTERVAL * 3, TimeProvider.System, ct);
                phase = "2 joining";
            }
            var started = Stopwatch.GetTimestamp();
            using var gate = new SemaphoreSlim(LoadOptions.MAX_JOINS_IN_FLIGHT);
            await Task.WhenAll(voters.Select(async voter =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    await voter.JoinAsync(ct);
                }
                finally
                {
                    gate.Release();
                }
            }));
            await progress.WriteLineAsync($"joined {voters.Count(v => v.Joined)} of {options.Voters} in {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s; {scenario.Action} over {options.Seconds} s");
            if (presenter != null)
            {
                phase = "3 connected";
                await Task.Delay(Presenter.PROBE_INTERVAL * 3, TimeProvider.System, ct);
                phase = "4 window";
            }
            var window = TimeSpan.FromSeconds(options.Seconds);
            var close = presenter != null && options.CloseAt is { } closeAt && scenario is PollScenario poll ? CloseLaterAsync(presenter, closeAt, poll.State, ct) : Task.CompletedTask;
            await Task.WhenAll(voters.Where(v => v.Joined).Select(async voter =>
            {
                var offset = window * Dice.NextDouble();
                await Task.Delay(offset, TimeProvider.System, ct);
                await voter.ActAsync(window - offset, ct);
            }));
            await close;
            await stopProbes.CancelAsync();
            await probes;
            if (presenter != null)
            {
                await progress.WriteAsync(presenter.Table());
            }
            return scenario.Report();
        }
        finally
        {
            await stopProbes.CancelAsync();
            await Task.WhenAll(voters.Select(v => v.DisposeAsync().AsTask()));
        }
    }

    private static async Task CloseLaterAsync(Presenter presenter, double seconds, RunState state, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds), TimeProvider.System, ct);
        await presenter.CloseAsync(state, ct);
    }
}
