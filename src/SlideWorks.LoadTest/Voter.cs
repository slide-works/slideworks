using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace SlideWorks.LoadTest;

// One guest, as a phone is: a sw_guest cookie of its own, the guest poll view, a hub connection over WebSockets that has
// called JoinAudience, and one answer. It measures the answer's write (request sent to response read) and then how long the
// first pollTally on its own connection whose answered count includes its answer took to arrive. It sends no Origin, as a
// client that isn't a browser doesn't. Every refusal is counted by kind in RunState, never swallowed.
public sealed class Voter(LoadOptions options, HttpClient http, RunState state) : IAsyncDisposable
{
    private const string HUB_PATH = "/hubs/deck";
    private const string AUDIENCE_EVENT = "audienceEvent";

    private readonly string _cookie = $"sw_guest={Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
    private readonly object _lock = new();
    private HubConnection? _hub;
    private PollWire.Activity? _activity;
    private int _revision;
    private int _seenAnswered;
    private int _awaited = int.MaxValue;
    private long _reachedAt;
    private TaskCompletionSource? _reached;

    public bool Joined { get; private set; }

    // The view, then the hub: the voter is joined once JoinAudience has returned. False, with the error counted, when any step
    // is refused.
    public async Task<bool> JoinAsync(CancellationToken ct)
    {
        try
        {
            var view = await ReadViewAsync(ct);
            if (view is null)
            {
                return false;
            }
            _activity = view.Activity;
            _revision = view.Mine?.Revision ?? 0;
            _hub = new HubConnectionBuilder()
                .WithUrl(new Uri(options.Url, HUB_PATH), o =>
                {
                    o.Transports = HttpTransportType.WebSockets;
                    o.SkipNegotiation = false;
                    o.Headers["Cookie"] = _cookie;
                })
                .Build();
            _hub.On<JsonElement>(AUDIENCE_EVENT, OnAudienceEvent);
            await _hub.StartAsync(ct);
            await _hub.InvokeAsync<JsonElement>("JoinAudience", options.Code, ct);
            Joined = true;
            state.RecordJoined();
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            state.RecordError($"join: {Describe(e)}");
            return false;
        }
    }

    // Sends the one answer, then waits for the tally that includes it. Call it only after JoinAsync returned true.
    public async Task AnswerAsync(CancellationToken ct)
    {
        if (_activity?.Round is not { Length: > 0 } round || _activity.Options is not { Count: > 0 } optionList || _activity.Phase != "open")
        {
            state.RecordError($"answer: no open round (phase {_activity?.Phase ?? "none"})");
            return;
        }
        var picks = PickChoice.For(options.Pick, optionList.Count);
        var started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/join/{options.Code}/poll/answer")
            {
                Content = JsonContent.Create(new { round, picks, revision = _revision }),
            };
            request.Headers.Add("Cookie", _cookie);
            response = await http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            state.RecordError($"answer: {Describe(e)}");
            return;
        }
        using (response)
        {
            var writeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                state.RecordError($"answer: {await RefusalAsync(response, ct)}");
                return;
            }
            var acknowledgedAt = Stopwatch.GetTimestamp();
            await WaitForResultsAsync(state.RecordWrite(writeMs), acknowledgedAt, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }
    }

    private async Task<PollWire.View?> ReadViewAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/join/{options.Code}/poll");
        request.Headers.Add("Cookie", _cookie);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            state.RecordError($"join: view {await RefusalAsync(response, ct)}");
            return null;
        }
        return await response.Content.ReadFromJsonAsync<PollWire.View>(PollWire.JSON, ct);
    }

    // Waits until a tally with at least `awaited` answered has been seen on this connection, up to the results timeout. One
    // that arrived before the 200 counts as zero.
    private async Task WaitForResultsAsync(int awaited, long acknowledgedAt, CancellationToken ct)
    {
        Task reached;
        lock (_lock)
        {
            _awaited = awaited;
            if (_seenAnswered >= awaited)
            {
                state.RecordResults(0);
                return;
            }
            _reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            reached = _reached.Task;
        }
        var timeout = TimeSpan.FromSeconds(options.ResultsTimeoutSeconds);
        var winner = await Task.WhenAny(reached, Task.Delay(timeout, TimeProvider.System, ct));
        ct.ThrowIfCancellationRequested();
        if (winner == reached)
        {
            lock (_lock)
            {
                state.RecordResults(Math.Max(0, Stopwatch.GetElapsedTime(acknowledgedAt, _reachedAt).TotalMilliseconds));
            }
            return;
        }
        state.RecordResultsTimeout(timeout.TotalMilliseconds);
    }

    // Only a pollTally for this round counts. The harness reads leniently: other events and unknown fields are ignored.
    private void OnAudienceEvent(JsonElement e)
    {
        if (!e.TryGetProperty("type", out var type) || type.GetString() != "pollTally"
            || !e.TryGetProperty("answered", out var answered) || !answered.TryGetInt32(out var count)
            || (e.TryGetProperty("round", out var round) && round.GetString() != _activity?.Round))
        {
            return;
        }
        lock (_lock)
        {
            _seenAnswered = Math.Max(_seenAnswered, count);
            if (_seenAnswered >= _awaited && _reached is { Task.IsCompleted: false })
            {
                _reachedAt = Stopwatch.GetTimestamp();
                _reached.TrySetResult();
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
}
