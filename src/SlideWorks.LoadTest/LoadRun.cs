using System.Diagnostics;
using System.Net.Http.Json;

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
        var state = new RunState(await BaselineAsync(options, http, ct));
        var voters = Enumerable.Range(0, options.Voters).Select(_ => new Voter(options, http, state)).ToList();
        try
        {
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
            var window = TimeSpan.FromSeconds(options.Seconds);
            await Task.WhenAll(voters.Where(v => v.Joined).Select(async voter =>
            {
                await Task.Delay(window * Dice.NextDouble(), TimeProvider.System, ct);
                await voter.AnswerAsync(ct);
            }));
            return LoadReport.From(options, state);
        }
        finally
        {
            await Task.WhenAll(voters.Select(v => v.DisposeAsync().AsTask()));
        }
    }

    // A fresh guest reads the poll first: a run against a poll that is not open would only measure refusals, so it stops, and
    // the tally it finds is what the voters' answers are counted from.
    private static async Task<int> BaselineAsync(LoadOptions options, HttpClient http, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/poll");
        request.Headers.Add("Cookie", $"sw_guest={Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))}");
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
        return view.Tally?.Answered ?? 0;
    }
}
