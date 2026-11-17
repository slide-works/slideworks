using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// One run: read the poll once to learn it is open, join every voter (at most MAX_JOINS_IN_FLIGHT at a time, which keeps the
// app's join limiter under its in-flight cap), then give each voter a random moment inside the window to answer, and wait
// for the results.
public static class LoadRun
{
    public static async Task<LoadReport> RunAsync(LoadOptions options, TextWriter progress, CancellationToken ct)
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
        var state = new RunState(await BaselineAsync(options, http, ct));
        var voters = Enumerable.Range(0, options.Voters).Select(_ => new Voter(options, http, state)).ToList();
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
            await progress.WriteLineAsync($"joined {state.Joined} of {options.Voters} in {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s; answering over {options.Seconds} s");
            if (presenter != null)
            {
                phase = "3 connected";
                await Task.Delay(Presenter.PROBE_INTERVAL * 3, TimeProvider.System, ct);
                phase = "4 window";
            }
            var window = TimeSpan.FromSeconds(options.Seconds);
            var close = presenter != null && options.CloseAt is { } closeAt ? CloseLaterAsync(presenter, closeAt, state, ct) : Task.CompletedTask;
            await Task.WhenAll(voters.Where(v => v.Joined).Select(async voter =>
            {
                await Task.Delay(window * Dice.NextDouble(), TimeProvider.System, ct);
                await voter.AnswerAsync(ct);
            }));
            await close;
            await stopProbes.CancelAsync();
            await probes;
            if (presenter != null)
            {
                await progress.WriteAsync(presenter.Table());
            }
            return LoadReport.From(options, state);
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

    // A fresh guest reads the poll first: a run against a poll that is not open would only measure refusals, so it stops, and
    // the tally it finds is what the voters' answers are counted from.
    private static async Task<int> BaselineAsync(LoadOptions options, HttpClient http, CancellationToken ct)
    {
        var cookie = $"sw_guest={Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))}";
        // The app gives the poll view only to a guest on the session's roster, so this guest joins the audience first.
        await using var hub = Presenter.Hub(options.Url, cookie);
        await hub.StartAsync(ct);
        await hub.InvokeAsync<System.Text.Json.JsonElement>("JoinAudience", options.Code, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/poll");
        request.Headers.Add("Cookie", cookie);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GET /api/join/{options.Code}/poll answered {(int)response.StatusCode}. Is the code live?");
        }
        var view = await response.Content.ReadFromJsonAsync<PollWire.View>(PollWire.JSON, ct);
        if (view?.Activity is not { Phase: "open", Round: not null })
        {
            throw new InvalidOperationException($"The poll is not open (phase {view?.Activity?.Phase ?? "none"}). Open a poll round on the deck and run again.");
        }
        // The tally a guest gets follows the poll's results setting. Under any other the thresholds would start from 0 and the
        // results would read low, so the run is refused unless tallies are not expected at all.
        if (!options.NoResults && !string.Equals(view.Activity.Results, "live", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The poll's results setting is '{view.Activity.Results ?? "unknown"}', not 'live', so the results timing would read low. Set the poll to live results, or pass {LoadOptions.NO_RESULTS}.");
        }
        return view.Tally?.Answered ?? 0;
    }
}
