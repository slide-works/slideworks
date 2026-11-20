using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// The manifest an agent sends is only the fields that change: each top-level key replaces the stored one. This puts the
// patch over the stored manifest and hands the whole text to ITemplateDrafts, which parses, checks and stores it. A patch
// that is not an object, or that sets a key to null, is refused here with manifest_invalid, before anything is written.
public static class TemplateManifestPatch
{
    public static void Check(JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, null, ("reason", "give the manifest as one JSON object of the fields that change"));
        }
        var nulled = patch.EnumerateObject().Cast<JsonProperty?>().FirstOrDefault(f => f!.Value.Value.ValueKind == JsonValueKind.Null);
        if (nulled is { } field)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, field.Name, ("reason", "a field cannot be null; leave it out to keep it"));
        }
    }

    public static string Apply(JsonElement current, JsonElement patch)
    {
        Check(patch);
        var merged = JsonNode.Parse(current.GetRawText())!.AsObject();
        foreach (var field in patch.EnumerateObject())
        {
            merged[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        }
        return merged.ToJsonString();
    }
}
