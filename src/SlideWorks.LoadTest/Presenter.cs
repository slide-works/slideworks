using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// The person running the deck, for a run given --owner-cookie, --deck and --slide (the cookie of someone who can present the
// deck, such as a signed-in session's). It joins the presentation as the presenter window does and moves to the poll slide, so
// the arrival rule opens the round. During the run it probes what a presenter and a newcomer feel, one probe every
// PROBE_INTERVAL in three phases (before any voter joins, with every voter connected, and inside the answer window):
//   step:   a presenter move on the poll slide (UpdatePresentation, the deep-dive switch flipped), hub call to its return;
//   player: a fresh owner connection's first join (start + JoinPresentation as the audience window), as the player's;
//   guest:  a fresh guest's join (GET poll view + start + JoinAudience), as a phone's.
// With --close-at it also closes the poll that many seconds into the window and times the close.
// Probes are their own connections, never voters: they add no samples to the gate's numbers.
public sealed class Presenter(LoadOptions options, HttpClient http) : IAsyncDisposable
{
    public static readonly TimeSpan PROBE_INTERVAL = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, List<double>> _samples = new(StringComparer.Ordinal);
    private HubConnection? _hub;
    private bool _skipDeepDives;

    public string? CloseLine { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        _hub = OwnerHub();
        await _hub.StartAsync(ct);
        await _hub.InvokeAsync<JsonElement>("JoinPresentation", options.Deck, "load-presenter", "presenter", "Load presenter", "UTC", null, ct);
        await MoveAsync(ct);
    }

    // Probes until ct is cancelled, filing each under the phase phaseOf names at the probe's start.
    public async Task ProbeAsync(Func<string> phaseOf, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PROBE_INTERVAL);
        try
        {
            do
            {
                var phase = phaseOf();
                await TimeAsync($"{phase} step", () => MoveAsync(ct));
                await TimeAsync($"{phase} player", () => PlayerJoinAsync(ct));
                await TimeAsync($"{phase} guest", () => GuestJoinAsync(ct));
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The run is over.
        }
    }

    public async Task CloseAsync(RunState state, CancellationToken ct)
    {
        var acknowledgedBefore = state.Answered;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/decks/{options.Deck}/polls/{options.Slide}/close");
        request.Headers.Add("Cookie", options.OwnerCookie);
        var started = Stopwatch.GetTimestamp();
        using var response = await http.SendAsync(request, ct);
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(PollWire.JSON, ct);
        var answered = body.TryGetProperty("answered", out var a) ? a.GetInt32().ToString(CultureInfo.InvariantCulture) : "?";
        CloseLine = $"close {(int)response.StatusCode} in {ms:F0} ms; answers acknowledged when sent {acknowledgedBefore}, after {state.Answered}; round's final answered {answered} (baseline {state.BaselineAnswered})";
    }

    public string Table()
    {
        var text = new StringBuilder();
        foreach (var (name, samples) in _samples.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"probe {name,-16} n {samples.Count,3}   p50 {Percentile.Of(samples, 50):F0} ms   p95 {Percentile.Of(samples, 95):F0} ms   max {samples.Max():F0} ms");
        }
        if (CloseLine != null)
        {
            text.AppendLine(CloseLine);
        }
        return text.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
        }
    }

    private Task MoveAsync(CancellationToken ct)
    {
        _skipDeepDives = !_skipDeepDives;
        return _hub!.InvokeAsync<JsonElement>(
            "UpdatePresentation", options.Deck, new { slide = options.Slide, step = 0, black = false, skipDeepDives = _skipDeepDives }, ct);
    }

    private async Task PlayerJoinAsync(CancellationToken ct)
    {
        await using var hub = OwnerHub();
        await hub.StartAsync(ct);
        await hub.InvokeAsync<JsonElement>("JoinPresentation", options.Deck, $"probe-{Guid.NewGuid():N}", "audience", "Probe", "UTC", null, ct);
    }

    private async Task GuestJoinAsync(CancellationToken ct)
    {
        var cookie = $"sw_guest={Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
        using (var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/poll"))
        {
            request.Headers.Add("Cookie", cookie);
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        await using var hub = Hub(cookie);
        await hub.StartAsync(ct);
        await hub.InvokeAsync<JsonElement>("JoinAudience", options.Code, ct);
    }

    private async Task TimeAsync(string name, Func<Task> probe)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await probe();
            Add(name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Add($"{name} failed", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private void Add(string name, double ms)
    {
        if (!_samples.TryGetValue(name, out var list))
        {
            list = [];
            _samples[name] = list;
        }
        list.Add(ms);
    }

    private HubConnection OwnerHub() => Hub(options.OwnerCookie!);

    private HubConnection Hub(string cookie) => new HubConnectionBuilder()
        .WithUrl(new Uri(options.Url, "/hubs/deck"), o =>
        {
            o.Transports = HttpTransportType.WebSockets;
            o.Headers["Cookie"] = cookie;
        })
        .Build();
}
