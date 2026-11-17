using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SlideWorks.WebApi.Tests.RateLimits;

// Test hosts only: TestServer gives every request a null remote address, so this startup filter, which runs ahead of the
// whole app pipeline, sets the connection's remote address from the X-Test-Remote-Address header, as the socket's peer
// address would be in production. A request without the header keeps no address.
public sealed class TestRemoteAddress : IStartupFilter
{
    public const string HEADER = "X-Test-Remote-Address";

    public static void AddTo(IServiceCollection services) => services.AddSingleton<IStartupFilter, TestRemoteAddress>();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HEADER, out var address))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            }
            return nextMiddleware(context);
        });
        next(app);
    };
}
