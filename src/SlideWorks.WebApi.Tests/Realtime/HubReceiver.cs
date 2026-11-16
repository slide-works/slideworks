using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// One connection to /hubs/deck over a real WebSocket on the test server, and the events it has received. A browser's
// connection carries its session cookie on both the negotiate request and the WebSocket upgrade; this one names its
// TestUser in the test sign-in header on both instead. Anonymous sends nothing.
//
// "Receives nothing" is proved without waiting: a later event for another deck goes through the same relay in order,
// so once that arrives everything published before it would already have been delivered.
public sealed class HubReceiver : IAsyncDisposable
{
    private readonly Channel<JsonElement> _events = Channel.CreateUnbounded<JsonElement>();

    private HubReceiver(WebApplicationFactory<ApiMarker> factory, TestUser user)
    {
        var server = factory.Server;
        Connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, DeckHub.PATH), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (!user.IsAnonymous)
                {
                    options.Headers[TestAuthScheme.HEADER] = user.Email;
                }
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    if (!user.IsAnonymous)
                    {
                        client.ConfigureRequest = request => request.Headers[TestAuthScheme.HEADER] = user.Email;
                    }
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .Build();
        Connection.On<JsonElement>(DeckHub.EVENT_METHOD, e => _events.Writer.TryWrite(e));
    }

    public HubConnection Connection { get; }

    public static async Task<HubReceiver> StartAsync(WebApplicationFactory<ApiMarker> factory, TestUser user)
    {
        var receiver = new HubReceiver(factory, user);
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

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
