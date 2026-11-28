using Microsoft.AspNetCore.Http.Connections;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// How a HubReceiver connects (K5, K7): as a TestUser (the test sign-in header), with a raw Cookie header, with a device
// secret as an Authorization bearer header, with a device secret as the access_token query a browser's WebSocket uses, and
// with a guest id as the guest query. Each credential rides on both the negotiate request and the WebSocket upgrade.
public sealed record HubReceiverOptions
{
    public TestUser User { get; init; } = TestUser.Anonymous;

    public HttpTransportType Transport { get; init; } = HttpTransportType.WebSockets;

    public string? Cookie { get; init; }

    public string? Bearer { get; init; }

    public string? AccessToken { get; init; }

    public string? Guest { get; init; }
}
