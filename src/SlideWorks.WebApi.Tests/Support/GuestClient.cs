using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// An audience guest on the hub: anonymous, with a sw_guest cookie it mints itself (GuestCookie.NewId, the format the server
// issues and accepts), connected over the chosen transport. Pass noCookie for a guest the server has never
// seen. Tests of anything a guest does over the hub use this, never their own connection.
public sealed class GuestClient : IAsyncDisposable
{
    private readonly HubReceiver _receiver;

    private GuestClient(HubReceiver receiver, string guestId)
    {
        _receiver = receiver;
        GuestId = guestId;
    }

    public string GuestId { get; }

    public HubConnection Connection => _receiver.Connection;

    public static async Task<GuestClient> ConnectAsync(
        WebApplicationFactory<ApiMarker> factory,
        HttpTransportType transport = HttpTransportType.WebSockets,
        string? guestId = null,
        bool noCookie = false)
    {
        var id = guestId ?? GuestCookie.NewId();
        var receiver = await HubReceiver.StartAsync(factory, TestUser.Anonymous, transport, noCookie ? null : $"{GuestCookie.NAME}={id}");
        return new GuestClient(receiver, id);
    }

    public Task<JsonElement> NextAudienceAsync() => _receiver.NextAudienceAsync();

    public ValueTask DisposeAsync() => _receiver.DisposeAsync();
}
