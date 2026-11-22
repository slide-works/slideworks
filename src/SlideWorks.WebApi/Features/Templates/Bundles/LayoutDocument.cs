using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Templates.Bundles;

// A custom layout's upload document, the one file stored for it: {"markup": string, "css": string, "slots": object}.
// A bundle holds it as three files beside each other, <stem>.html, <stem>.css and <stem>.json.
internal sealed record LayoutDocument(
    [property: JsonPropertyName("markup")] string Markup,
    [property: JsonPropertyName("css")] string Css,
    [property: JsonPropertyName("slots")] JsonElement Slots)
{
    public const string HTML = ".html";
    public const string CSS = ".css";
    public const string JSON = ".json";

    private static readonly UTF8Encoding STRICT = new(false, true);

    // The document for the three files' bytes. Anything that is not text, or slots that are not a JSON object, is
    // bundle_invalid.
    public static LayoutDocument Of(string stem, byte[] html, byte[] css, byte[] slots)
    {
        try
        {
            using var parsed = JsonDocument.Parse(slots);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw BundleArchive.Invalid($"{stem}{JSON} must hold a JSON object");
            }
            return new LayoutDocument(STRICT.GetString(html), STRICT.GetString(css), parsed.RootElement.Clone());
        }
        catch (Exception e) when (e is JsonException or DecoderFallbackException)
        {
            throw BundleArchive.Invalid($"the files of layout {stem} are not valid text or JSON");
        }
    }

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this);

    // The three files back out of a stored document.
    public static LayoutDocument Read(byte[] stored)
    {
        using var document = JsonDocument.Parse(stored);
        var root = document.RootElement;
        return new LayoutDocument(root.GetProperty("markup").GetString()!, root.GetProperty("css").GetString()!, root.GetProperty("slots").Clone());
    }
}
