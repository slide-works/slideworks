using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// An http or https address that the player draws as a link (K12). The text becomes an href, so the
// checks run on the raw string before any URL parser sees it: the browser's parser and .NET's disagree
// on odd inputs (a tab inside the scheme, a backslash for a slash, user info), and the rules below
// leave only strings both read the same way. The cases are in src/contracts/link-cases.json, which the
// client's check runs too. A link is never trimmed or rewritten: it is stored as it was sent or refused.
public sealed class LinkSlot(int max = LinkSlot.MAX_LENGTH) : ISlotType
{
    public const int MAX_LENGTH = 500;

    private const string NOT_AN_ADDRESS = "must be an http or https address, such as https://example.com/page";
    private const string USER_INFO = "must not hold a user name or password";

    private static readonly string[] _schemes = ["http://", "https://"];

    public int MaxLength { get; } = max is >= 1 and <= MAX_LENGTH
        ? max
        : throw new ArgumentOutOfRangeException(nameof(max), max, $"A link's limit is 1 to {MAX_LENGTH} characters");

    public string Expected => "a link";

    public string Kind => "link";

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["format"] = "uri",
        ["maxLength"] = MaxLength,
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var problem = Problem(value.GetString() ?? "", MaxLength);
        if (problem is not null)
        {
            validation.Fail(path, problem);
        }
    }

    // What is wrong with the text as a link, or null when it is one. The order is the contract's: the
    // length first, so the size of the text never sets the cost of the other checks.
    public static string? Problem(string text, int max = MAX_LENGTH)
    {
        if (text.Length > max)
        {
            return $"at most {max} characters (got {text.Length})";
        }

        if (text.Any(c => c is < '!' or > '~'))
        {
            return "must not hold spaces, control characters or characters outside ASCII; write an international host in punycode";
        }

        if (text.Contains('\\'))
        {
            return "must not hold a backslash";
        }

        var scheme = _schemes.FirstOrDefault(s => text.StartsWith(s, StringComparison.OrdinalIgnoreCase));
        if (scheme is null || text.Length == scheme.Length || text[scheme.Length] is '/' or '@' or ':')
        {
            return NOT_AN_ADDRESS;
        }

        var hostEnd = text.IndexOfAny(['/', '?', '#'], scheme.Length);
        var authority = hostEnd < 0 ? text[scheme.Length..] : text[scheme.Length..hostEnd];
        if (authority.Contains('@'))
        {
            return USER_INFO;
        }

        // The parser has the last word: it must agree that this is an http or https address with a host.
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Host.Length == 0)
        {
            return NOT_AN_ADDRESS;
        }

        return uri.UserInfo.Length == 0 ? null : USER_INFO;
    }
}
