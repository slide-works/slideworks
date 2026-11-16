using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// SLIDEWORKS_TRUSTED_PROXIES (opt-in, unset by default): a comma-separated list of CIDRs, such as
// "172.18.0.0/16, fd00::/8". A bare address means just that address (/32 or /128). When it's set, a request whose remote
// address is inside one of them takes its client address from the last X-Forwarded-For entry, the one that proxy
// appended: ForwardLimit 1, so one hop and no further. That is the address the per-caller rate limits key on. Only
// X-Forwarded-For is read: scheme and host still come only from SLIDEWORKS_PUBLIC_URL (PublicOriginMiddleware), so
// X-Forwarded-Proto and X-Forwarded-Host are never trusted. A request from any other address, or with no remote address,
// keeps its own address and its X-Forwarded-For is ignored. An entry that isn't a CIDR or an address stops startup.
public static class TrustedProxiesSetup
{
    public const string CONFIG_KEY = "SLIDEWORKS_TRUSTED_PROXIES";

    // First in the pipeline, so everything after it, the rate limits included, sees the client's address.
    public static WebApplication UseSlideWorksTrustedProxies(this WebApplication app)
    {
        var networks = Parse(app.Configuration[CONFIG_KEY]);
        if (networks.Count == 0)
        {
            return app;
        }
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1,
        };
        // The framework trusts loopback by default; only the configured networks are trusted here.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(network);
        }
        // The forwarded-headers middleware trusts a request with no remote address at all; SlideWorks doesn't.
        app.UseWhen(context => context.Connection.RemoteIpAddress != null, branch => branch.UseForwardedHeaders(options));
        return app;
    }

    public static IReadOnlyList<IPNetwork> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }
        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(ParseOne).ToList();
    }

    private static IPNetwork ParseOne(string entry)
    {
        if (!entry.Contains('/', StringComparison.Ordinal) && IPAddress.TryParse(entry, out var address))
        {
            return new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);
        }
        if (IPNetwork.TryParse(entry, out var network))
        {
            return network;
        }
        throw new InvalidOperationException(
            $"{CONFIG_KEY} has '{entry}', which isn't a CIDR such as 172.18.0.0/16 or fd00::/8, or a single address. "
            + "Separate entries with commas.");
    }
}
