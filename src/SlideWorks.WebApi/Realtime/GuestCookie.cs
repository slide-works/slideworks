using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Realtime;

// The sw_guest cookie (J3, D12): a guest's identity, 128 random bits. The hub reads it from the connection's request; a
// missing or malformed one is no guest.
public static partial class GuestCookie
{
    public const string NAME = "sw_guest";

    // The id of the guest on this request, or null when there is none or it isn't well formed (16 to 64 URL-safe characters).
    public static string? IdOf(HttpContext? context)
    {
        var value = context?.Request.Cookies[NAME];
        return value != null && WellFormed().IsMatch(value) ? value : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex WellFormed();
}
