using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// One guest of the qa scenario, as a phone is: a sw_guest cookie of its own, a hub connection over WebSockets that has called
// JoinAudience, then the questions view. It upvotes up to three open questions, one PUT each with the revision the last
// answer gave, then taps ten reactions spread over what is left of the window, and last waits for the questionTally that
// includes each upvote. It sends no Origin, as a client that isn't a browser doesn't. Every refusal is counted by kind in
// RunState, one per guest, never swallowed, and a refused guest stops acting.
public sealed class QaGuest(LoadOptions options, Uri hubBase, HttpClient http, RunState state, QaTotals totals, QaBaseline baseline) : ILoadGuest
{
    public static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    public const int UPVOTES = 3;
    public const int TAPS = 10;

    private const string HUB_PATH = "/hubs/deck";
    private const string AUDIENCE_EVENT = "audienceEvent";
    private static readonly string[] EMOJI = ["thumbsUp", "heart", "laugh", "wow", "clap"];

    private readonly string _cookie = $"sw_guest={Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _seen = [];
    private readonly List<Awaited> _awaited = [];
    private HubConnection? _hub;
    private List<int> _choice = [];
    private int _revision;
    private bool _errored;
    private bool _dropped;
    private bool _done;
    private volatile bool _disposing;

    public bool Joined { get; private set; }

    public async Task<bool> JoinAsync(CancellationToken ct)
    {
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(new Uri(hubBase, HUB_PATH), o =>
                {
                    o.Transports = HttpTransportType.WebSockets;
                    o.SkipNegotiation = false;
                    o.Headers["Cookie"] = _cookie;
                })
                .Build();
            _hub.On<JsonElement>(AUDIENCE_EVENT, OnAudienceEvent);
            _hub.Closed += OnClosed;
            await StartHubAsync(ct);
            await _hub.InvokeAsync<JsonElement>("JoinAudience", options.Code, ct);
            var view = await ReadViewAsync(ct);
            if (view is null)
            {
                return false;
            }
            if (view.Room?.Round != baseline.Round)
            {
                Fail($"join: round is {view.Room?.Round ?? "none"}, not the one the run started on");
                return false;
            }
            _revision = view.Mine?.Revision ?? 0;
            _choice = [.. (view.Questions ?? []).Where(q => q.State == "open" && !q.Yours).Select(q => q.Number).OrderBy(_ => Dice.Next(int.MaxValue)).Take(UPVOTES)];
            if (_choice.Count == 0)
            {
                Fail("join: no open question");
                return false;
            }
            Joined = true;
            state.RecordJoined();
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Fail($"join: {Describe(e)}");
            return false;
        }
    }

    public async Task ActAsync(TimeSpan remaining, CancellationToken ct)
    {
        if (!await UpvoteAsync(ct) || !await TapAsync(remaining, ct))
        {
            return;
        }
        await WaitForTalliesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        _disposing = true;
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }
    }

    // One PUT per question, each carrying every pick so far (the vote replaces the guest's picks) and the revision the last 200
    // returned. False when one is refused.
    private async Task<bool> UpvoteAsync(CancellationToken ct)
    {
        var picks = new List<int>();
        foreach (var number in _choice)
        {
            picks.Add(number);
            var started = Stopwatch.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{options.Code}/questions/votes")
            {
                Content = JsonContent.Create(new { round = baseline.Round, picks, revision = _revision }),
            };
            request.Headers.Add("Cookie", _cookie);
            using var response = await SendAsync(request, "vote", ct);
            if (response is null)
            {
                return false;
            }
            var writeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Fail($"vote: {await RefusalAsync(response, ct)}");
                return false;
            }
            var acknowledgedAt = Stopwatch.GetTimestamp();
            _revision = (await response.Content.ReadFromJsonAsync<QaWire.Votes>(JSON, ct))?.Revision ?? _revision;
            state.RecordWrite(writeMs);
            totals.RecordVote();
            if (!options.NoResults)
            {
                Await(number, baseline.Acknowledge(number), acknowledgedAt);
            }
        }
        return true;
    }

    // Ten taps, evenly spread over what is left of the window, each a different emoji in turn. False when one is refused.
    private async Task<bool> TapAsync(TimeSpan remaining, CancellationToken ct)
    {
        var gap = remaining / TAPS;
        for (var i = 0; i < TAPS; i++)
        {
            if (i > 0)
            {
                await Task.Delay(gap, TimeProvider.System, ct);
            }
            var started = Stopwatch.GetTimestamp();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/join/{options.Code}/reactions")
            {
                Content = JsonContent.Create(new { emoji = EMOJI[i % EMOJI.Length] }),
            };
            request.Headers.Add("Cookie", _cookie);
            using var response = await SendAsync(request, "tap", ct);
            if (response is null)
            {
                return false;
            }
            var writeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                Fail($"tap: {await RefusalAsync(response, ct)}");
                return false;
            }
            state.RecordWrite(writeMs);
            totals.RecordReaction();
        }
        return true;
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, string route, CancellationToken ct)
    {
        try
        {
            var response = await http.SendAsync(request, ct);
            totals.RecordOutcome(route, (int)response.StatusCode);
            return response;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Fail($"{route}: {Describe(e)}");
            return null;
        }
    }

    private async Task<QaWire.View?> ReadViewAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/questions");
        request.Headers.Add("Cookie", _cookie);
        using var response = await http.SendAsync(request, ct);
        totals.RecordOutcome("view", (int)response.StatusCode);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Fail($"join: view {await RefusalAsync(response, ct)}");
            return null;
        }
        return await response.Content.ReadFromJsonAsync<QaWire.View>(JSON, ct);
    }

    // Registers an upvote's wait: it is over once a tally has shown at least `expected` votes on its question. A tally that
    // arrived before the 200 counts as zero.
    private void Await(int number, int expected, long acknowledgedAt)
    {
        lock (_lock)
        {
            var wait = new Awaited(number, expected, acknowledgedAt);
            if (_seen.GetValueOrDefault(number) >= expected)
            {
                wait.Reach(acknowledgedAt);
            }
            _awaited.Add(wait);
        }
    }

    // Each upvote's tally gets the results timeout from its own 200. A wait that ran out counts at the full timeout, and the
    // guest is one error for it.
    private async Task WaitForTalliesAsync(CancellationToken ct)
    {
        List<Awaited> waits;
        lock (_lock)
        {
            waits = [.. _awaited];
        }
        var timeout = TimeSpan.FromSeconds(options.ResultsTimeoutSeconds);
        foreach (var wait in waits)
        {
            var left = timeout - Stopwatch.GetElapsedTime(wait.AcknowledgedAt);
            var winner = await Task.WhenAny(wait.Reached, Task.Delay(left < TimeSpan.Zero ? TimeSpan.Zero : left, TimeProvider.System, ct));
            ct.ThrowIfCancellationRequested();
            lock (_lock)
            {
                if (_dropped)
                {
                    return;
                }
                if (winner == wait.Reached)
                {
                    state.RecordResults(Math.Max(0, Stopwatch.GetElapsedTime(wait.AcknowledgedAt, wait.ReachedAt).TotalMilliseconds));
                }
                else
                {
                    state.RecordResultsTimeout(timeout.TotalMilliseconds);
                    Fail("results: timed out");
                }
            }
        }
        lock (_lock)
        {
            _done = true;
        }
    }

    private async Task StartHubAsync(CancellationToken ct)
    {
        try
        {
            await _hub!.StartAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            state.RecordDropped();
            throw;
        }
    }

    // A connection that closes before the guest has every tally, other than through our own dispose, is dropped: counted once,
    // and its waits end without a sample.
    private Task OnClosed(Exception? error)
    {
        lock (_lock)
        {
            if (_disposing || _done || _dropped)
            {
                return Task.CompletedTask;
            }
            _dropped = true;
            state.RecordDropped();
            Fail($"hub: connection closed{(error is null ? string.Empty : ": " + error.Message)}");
            foreach (var wait in _awaited)
            {
                wait.Abandon();
            }
        }
        return Task.CompletedTask;
    }

    private void Fail(string kind)
    {
        lock (_lock)
        {
            if (_errored)
            {
                return;
            }
            _errored = true;
            state.RecordError(kind);
        }
    }

    // Only a questionTally for the run's round counts; the harness reads leniently, so other events and unknown fields are
    // ignored. The latest count seen per question is kept, so an upvote registered later still finds a tally that came first.
    private void OnAudienceEvent(JsonElement e)
    {
        if (!e.TryGetProperty("type", out var type) || type.GetString() != "questionTally"
            || (e.TryGetProperty("round", out var round) && round.GetString() != baseline.Round)
            || !e.TryGetProperty("votes", out var votes) || votes.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        var reachedAt = Stopwatch.GetTimestamp();
        lock (_lock)
        {
            foreach (var entry in votes.EnumerateArray())
            {
                if (entry.TryGetProperty("number", out var number) && number.TryGetInt32(out var n)
                    && entry.TryGetProperty("votes", out var count) && count.TryGetInt32(out var c))
                {
                    _seen[n] = Math.Max(_seen.GetValueOrDefault(n), c);
                }
            }
            foreach (var wait in _awaited.Where(w => !w.IsReached && _seen.GetValueOrDefault(w.Number) >= w.Expected))
            {
                wait.Reach(reachedAt);
            }
        }
    }

    private static async Task<string> RefusalAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? error = null;
        try
        {
            error = (await response.Content.ReadFromJsonAsync<PollWire.Refusal>(PollWire.JSON, ct))?.Error;
        }
        catch (JsonException)
        {
            // Not a JSON body: the status alone names the refusal.
        }
        return error is null ? $"{(int)response.StatusCode}" : $"{(int)response.StatusCode} {error}";
    }

    private static string Describe(Exception e) => e is HttpRequestException ? $"http: {e.Message}" : e.GetType().Name + ": " + e.Message;

    // One upvote waiting for its tally.
    private sealed class Awaited(int number, int expected, long acknowledgedAt)
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Number { get; } = number;

        public int Expected { get; } = expected;

        public long AcknowledgedAt { get; } = acknowledgedAt;

        public long ReachedAt { get; private set; }

        public bool IsReached => _reached.Task.IsCompleted;

        public Task Reached => _reached.Task;

        public void Reach(long at)
        {
            ReachedAt = at;
            _reached.TrySetResult();
        }

        public void Abandon() => _reached.TrySetResult();
    }
}
