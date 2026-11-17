using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Features.Join;

// The sw_guest cookie (J3, D12): a guest's identity for one phone. 128 random bits as 32 lowercase hex digits, HttpOnly,
// SameSite=Lax, Secure on https, Path=/, Max-Age 12 hours. It is scoped to the host that set it, and it is not a login:
// it only says "this is a guest", so a value that isn't one of ours is treated as no cookie.
public static partial class GuestCookie
{
    public const string NAME = "sw_guest";

    public static readonly TimeSpan LIFETIME = TimeSpan.FromHours(12);

    private const int ID_BYTES = 16;

    // The guest id the request carries, or null when it has none or it isn't one we minted.
    public static string? From(HttpRequest request)
        => request.Cookies.TryGetValue(NAME, out var value) && IdPattern().IsMatch(value) ? value : null;

    // Sets a new guest cookie on the response unless the request already carries a valid one.
    public static void EnsureOn(HttpRequest request, HttpResponse response)
    {
        if (From(request) is not null)
        {
            return;
        }
        // S2092 wants Secure set unconditionally. It is set whenever the request is https, which PublicOriginMiddleware takes
        // from the public URL; a plain-http local run still gets the cookie.
#pragma warning disable S2092
        response.Cookies.Append(NAME, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ID_BYTES)), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = request.IsHttps,
            Path = "/",
            MaxAge = LIFETIME,
            IsEssential = true,
        });
#pragma warning restore S2092
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex IdPattern();
}
