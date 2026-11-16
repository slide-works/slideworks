using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Realtime;

// One connection to /hubs/deck over a real WebSocket on the test server, and the events it has received. It connects
// the way the browser's SignalR client does: the negotiate request carries the token as a bearer header, and the
// WebSocket upgrade (where a browser can't set headers) carries it as ?access_token=.
//
// "Receives nothing" is proved without waiting: a later event for another deck goes through the same relay in order,
// so once that arrives everything published before it would already have been delivered.
public sealed class HubReceiver : IAsyncDisposable
{
    private readonly Channel<JsonElement> _events = Channel.CreateUnbounded<JsonElement>();

    private HubReceiver(WebApplicationFactory<ApiMarker> factory, string? token)
    {
        var server = factory.Server;
        Connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, DeckHub.PATH), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(token);
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await server.CreateWebSocketClient().ConnectAsync(WithToken(context.Uri, token), cancellationToken);
            })
            .Build();
        Connection.On<JsonElement>(DeckHub.EVENT_METHOD, e => _events.Writer.TryWrite(e));
    }

    public HubConnection Connection { get; }

    public static async Task<HubReceiver> StartAsync(WebApplicationFactory<ApiMarker> factory, string? token)
    {
        var receiver = new HubReceiver(factory, token);
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

    private static Uri WithToken(Uri uri, string? token)
    {
        if (token == null)
        {
            return uri;
        }
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri}{separator}access_token={Uri.EscapeDataString(token)}");
    }
}
