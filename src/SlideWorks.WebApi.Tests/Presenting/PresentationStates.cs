using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// One connection to /hubs/deck (a HubReceiver) and the "presentationState" messages it has received, with the P2 calls
// sent the way the browser sends them: enums as strings, arguments as plain JSON objects. Results come back as raw JSON.
public sealed class PresentationStates : IAsyncDisposable
{
    public const string LABEL = "Chrome on Windows";

    public const string TIME_ZONE = "Asia/Bangkok";

    private readonly Channel<JsonElement> _states = Channel.CreateUnbounded<JsonElement>();
    private readonly HubReceiver _receiver;

    private PresentationStates(HubReceiver receiver)
    {
        _receiver = receiver;
        receiver.Connection.On<JsonElement>(DeckHub.PRESENTATION_STATE_METHOD, state => _states.Writer.TryWrite(state));
    }

    public HubConnection Connection => _receiver.Connection;

    public static async Task<PresentationStates> ConnectAsync(WebApplicationFactory<ApiMarker> factory, TestUser user)
        => new(await HubReceiver.StartAsync(factory, user));

    // The PresentationJoined JSON: { state, clockMinutes }.
    public Task<JsonElement> JoinAsync(string deckId, string clientId, string role = "presenter", object? resume = null, string timeZone = TIME_ZONE, string label = LABEL)
        => Connection.InvokeAsync<JsonElement>("JoinPresentation", deckId, clientId, role, label, timeZone, resume);

    public Task<JsonElement> UpdateAsync(string deckId, int slide, int step, bool black = false, bool skipDeepDives = false)
        => Connection.InvokeAsync<JsonElement>("UpdatePresentation", deckId, new { slide, step, black, skipDeepDives });

    public Task<JsonElement> SetTimerAsync(string deckId, string command)
        => Connection.InvokeAsync<JsonElement>("SetTimer", deckId, command);

    public Task LeaveAsync(string deckId) => Connection.InvokeAsync("LeavePresentation", deckId);

    public async Task<JsonElement> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _states.Reader.ReadAsync(timeout.Token);
    }

    // The next "deckEvent" (after a JoinDeck on this connection), for proving that no state arrived. A call's result can
    // reach the test before the handler of a state sent ahead of it has run, but handlers run in order, so once a later
    // deck event has been read, Received() holds every state sent before it.
    public Task<JsonElement> NextEventAsync() => _receiver.NextAsync();

    // The states received so far and not yet read, without waiting. Read it after NextEventAsync (see there).
    public IReadOnlyList<JsonElement> Received()
    {
        var received = new List<JsonElement>();
        while (_states.Reader.TryRead(out var state))
        {
            received.Add(state);
        }
        return received;
    }

    public static IReadOnlyList<string> ClientIds(JsonElement state)
        => state.GetProperty("connected").EnumerateArray().Select(c => c.GetProperty("clientId").GetString()!).ToList();

    // Every property name anywhere in the JSON, for the "never carries notes" checks (P9).
    public static IEnumerable<string> PropertyNames(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Object => json.EnumerateObject().SelectMany(p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => json.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    public ValueTask DisposeAsync() => _receiver.DisposeAsync();
}
