using System.Text;
using System.Text.Json;
using SlideWorks.Core.Look.CustomLayouts;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// How a custom layout is stored: the Meta JSON of a template asset of kind layout,
// { "layout": <the compiled stored form>, "review": { "approved": bool, "overflow": string[], "contrast": string[] } }.
// The asset's file is the uploaded document and is never served.
public sealed record StoredLayout(CompiledLayout Layout, bool Approved, IReadOnlyList<string> Overflow, IReadOnlyList<string> Contrast)
{
    private const string LAYOUT = "layout";
    private const string REVIEW = "review";
    private const string APPROVED = "approved";
    private const string OVERFLOW = "overflow";
    private const string CONTRAST = "contrast";

    // A new upload: not approved, and nothing measured yet.
    public static StoredLayout New(CompiledLayout layout) => new(layout, false, [], []);

    // Strict and fail closed, as ICustomLayoutCompiler.Read is, and with the same three exceptions: a document that is
    // not exactly this shape throws JsonException, so a row that was tampered with is never read as approved. Two rules
    // are checked here so that no reader can forget them, each refusing with StoredLayoutRefusedException: a row with no
    // meta (the column is nullable) is not a layout, and the compiled tree's scope is the start of the layout asset's id
    // (LayoutScope), so a layout stored under another asset's id is never read.
    public static StoredLayout Read(string? metaJson, string assetId, ICustomLayoutCompiler compiler)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
        {
            throw new StoredLayoutRefusedException(StoredLayoutRefusedException.UNNAMED, "the asset holds no stored layout");
        }

        if (assetId is not { Length: >= LayoutScope.LENGTH } || !LayoutScope.IsValid(assetId[..LayoutScope.LENGTH]))
        {
            throw new StoredLayoutRefusedException(StoredLayoutRefusedException.UNNAMED, "the asset's id does not start with a scope");
        }

        using var document = JsonDocument.Parse(metaJson);
        var root = document.RootElement;
        OnlyKeys(root, [LAYOUT, REVIEW]);
        var review = root.GetProperty(REVIEW);
        OnlyKeys(review, [APPROVED, OVERFLOW, CONTRAST]);
        var approved = review.GetProperty(APPROVED);
        if (approved.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException("A stored layout's review says approved with true or false.");
        }

        var layout = compiler.Read(root.GetProperty(LAYOUT).GetRawText());
        if (!string.Equals(layout.Tree.Scope, assetId[..LayoutScope.LENGTH], StringComparison.Ordinal))
        {
            throw new StoredLayoutRefusedException(layout.Name, "its scope is not the start of its asset's id");
        }

        return new StoredLayout(layout, approved.GetBoolean(), Texts(review.GetProperty(OVERFLOW)), Texts(review.GetProperty(CONTRAST)));
    }

    public string Write()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(LAYOUT);
            writer.WriteRawValue(CompiledLayoutJson.Write(Layout));
            writer.WriteStartObject(REVIEW);
            writer.WriteBoolean(APPROVED, Approved);
            WriteTexts(writer, OVERFLOW, Overflow);
            WriteTexts(writer, CONTRAST, Contrast);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteTexts(Utf8JsonWriter writer, string name, IReadOnlyList<string> texts)
    {
        writer.WriteStartArray(name);
        foreach (var text in texts)
        {
            writer.WriteStringValue(text);
        }

        writer.WriteEndArray();
    }

    // Exactly these keys, each once. No message quotes a piece of the document.
    private static void OnlyKeys(JsonElement json, string[] keys)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A stored layout is made of objects.");
        }

        var names = json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
        if (!names.SequenceEqual(keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new JsonException("A stored layout has a key that is unknown, missing or written twice.");
        }
    }

    private static IReadOnlyList<string> Texts(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array || json.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new JsonException("A stored layout's review holds lists of strings.");
        }

        return [.. json.EnumerateArray().Select(item => item.GetString()!)];
    }
}
