using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// One connection to /hubs/deck over a real transport on the test server (WebSockets unless a test names another), and the
// events it has received: "deckEvent" through NextAsync and "audienceEvent" through NextAudienceAsync. A browser's
// connection carries its session cookie on both the negotiate request and the WebSocket upgrade; this one names its
// TestUser in the test sign-in header on both instead. Anonymous sends nothing.
//
// "Receives nothing" is proved without waiting: a later event for another deck goes through the same relay in order,
// so once that arrives everything published before it would already have been delivered.
public sealed class HubReceiver : IAsyncDisposable
{
    private readonly Channel<JsonElement> _events = Channel.CreateUnbounded<JsonElement>();

    private readonly Channel<JsonElement> _audienceEvents = Channel.CreateUnbounded<JsonElement>();

    private HubReceiver(WebApplicationFactory<ApiMarker> factory, TestUser user, HttpTransportType transport, string? cookie)
    {
        var server = factory.Server;
        Connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, DeckHub.PATH), options =>
            {
                options.Transports = transport;
                if (cookie != null)
                {
                    options.Headers["Cookie"] = cookie;
                }
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (!user.IsAnonymous)
                {
                    options.Headers[TestAuthScheme.HEADER] = user.Email;
                }
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    if (cookie != null)
                    {
                        client.ConfigureRequest = request => request.Headers["Cookie"] = cookie;
                    }
                    if (!user.IsAnonymous)
                    {
                        var withCookie = client.ConfigureRequest;
                        client.ConfigureRequest = request =>
                        {
                            withCookie?.Invoke(request);
                            request.Headers[TestAuthScheme.HEADER] = user.Email;
                        };
                    }
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .Build();
        Connection.On<JsonElement>(DeckHub.EVENT_METHOD, e => _events.Writer.TryWrite(e));
        Connection.On<JsonElement>(DeckHub.AUDIENCE_EVENT_METHOD, e => _audienceEvents.Writer.TryWrite(e));
    }

    public HubConnection Connection { get; }

    // cookie is a raw Cookie header value, as a guest's browser sends it (GuestClient mints it).
    public static async Task<HubReceiver> StartAsync(
        WebApplicationFactory<ApiMarker> factory,
        TestUser user,
        HttpTransportType transport = HttpTransportType.WebSockets,
        string? cookie = null)
    {
        var receiver = new HubReceiver(factory, user, transport, cookie);
        try
        {
            await receiver.Connection.StartAsync();
        }
        catch
        {
            await receiver.DisposeAsync();
            throw;
        }
        return receiver;
    }

    public async Task<JsonElement> NextAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _events.Reader.ReadAsync(timeout.Token);
    }

    public async Task<JsonElement> NextAudienceAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _audienceEvents.Reader.ReadAsync(timeout.Token);
    }

    // The next poll event ("pollTally" or "pollControl") a connection receives, read with its next-audience-event reader
    // (HubReceiver.NextAudienceAsync or GuestClient.NextAudienceAsync). Any other audience event before it (an audienceState
    // push, a joinedCount) is skipped; a poll event is never skipped, so a frame the connection should not have had is what
    // this returns, and a test proves "nothing else came" by publishing a marker after it and expecting the marker first.
    public static async Task<JsonElement> NextPollEventAsync(Func<Task<JsonElement>> nextAudienceEvent)
    {
        while (true)
        {
            var frame = await nextAudienceEvent();
            if (frame.GetProperty("type").GetString() is "pollTally" or "pollControl")
            {
                return frame;
            }
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
