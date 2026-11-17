using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Look;

// The body of PUT /api/decks/{id}/look and the arguments of set_look (L6 look-change.json, L7): { template?, palette?,
// logo?: imageId | null, draft?, chrome?: { tracker?, footer?, progress? } }. logo and chrome stay raw JSON so a key
// left out (leave it) differs from a key set to null (remove the logo, or reset a chrome key to the template's default).
// Both callers turn it into a LookChange here, so a malformed value gets the same message from both.
// S6964 (under-posting) is the point here: an absent logo or chrome must stay Undefined, which a nullable or required
// property can't express.
#pragma warning disable S6964
public sealed record LookChangeRequest(string? Template, string? Palette, JsonElement Logo, bool? Draft, JsonElement Chrome)
#pragma warning restore S6964
{
    private const string TRACKER = "tracker";
    private const string FOOTER = "footer";
    private const string PROGRESS = "progress";
    private const string JOIN_BAR = "joinBar";
    private static readonly string[] FOOTERS = ["author", "date", "off"];

    public LookChange ToChange() => new(Template, Palette, LogoOf(Logo), Draft, ChromeOf(Chrome));

    private static LogoChange? LogoOf(JsonElement logo) => logo.ValueKind switch
    {
        JsonValueKind.Undefined => null,
        JsonValueKind.Null => new LogoChange(null),
        JsonValueKind.String => new LogoChange(logo.GetString()),
        _ => throw new BadRequestException("logo: must be an image id, or null to remove the logo"),
    };

    // An object with no keys changes nothing, the same as leaving chrome out.
    private static ChromeChange? ChromeOf(JsonElement chrome)
    {
        if (chrome.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }
        if (chrome.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException("chrome: must be an object such as { \"tracker\": false }; set a key to null to reset it");
        }

        Change<bool?>? tracker = null;
        Change<string?>? footer = null;
        Change<bool?>? progress = null;
        bool? joinBar = null;
        foreach (var key in chrome.EnumerateObject())
        {
            switch (key.Name)
            {
                case TRACKER:
                    tracker = new Change<bool?>(Flag(key));
                    break;
                case FOOTER:
                    footer = new Change<string?>(Footer(key));
                    break;
                case PROGRESS:
                    progress = new Change<bool?>(Flag(key));
                    break;
                case JOIN_BAR:
                    joinBar = key.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? key.Value.GetBoolean()
                        : throw new BadRequestException("chrome.joinBar: must be true or false");
                    break;
                default:
                    throw new BadRequestException($"chrome: unknown key '{key.Name}'; expected tracker, footer or progress");
            }
        }
        return tracker is null && footer is null && progress is null && joinBar is null ? null : new ChromeChange(tracker, footer, progress, joinBar);
    }

    private static bool? Flag(JsonProperty key) => key.Value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new BadRequestException($"chrome.{key.Name}: must be true, false or null"),
    };

    private static string? Footer(JsonProperty key)
    {
        if (key.Value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var value = key.Value.ValueKind == JsonValueKind.String ? key.Value.GetString() : null;
        return value is not null && FOOTERS.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new BadRequestException($"chrome.footer: must be {string.Join(", ", FOOTERS.Select(f => $"'{f}'"))} or null");
    }
}
