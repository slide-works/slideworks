using System.Net;
using System.Net.Sockets;

namespace SlideWorks.WebApi.Infrastructure.RateLimits;

// The key an anonymous endpoint's rate limit counts a caller under, from the request's remote address. Behind a trusted
// proxy (SLIDEWORKS_TRUSTED_PROXIES) that address is already the client's, from X-Forwarded-For. An IPv4 client that
// reaches a dual-stack socket as ::ffff:a.b.c.d is the same caller as a.b.c.d. An IPv6 host normally holds a whole /64,
// so it is keyed by that /64: rotating addresses inside it doesn't buy a new budget. "unknown" only when there is no
// remote address at all.
public static class CallerKey
{
    public const string UNKNOWN = "unknown";

    private const int IPV6_PREFIX_BYTES = 8;

    public static string For(IPAddress? address)
    {
        if (address == null)
        {
            return UNKNOWN;
        }
        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }
        // A new address from the bytes also drops any scope id (fe80::1%eth0).
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, IPV6_PREFIX_BYTES, bytes.Length - IPV6_PREFIX_BYTES);
        return $"{new IPAddress(bytes)}/64";
    }
}
