using System.Security.Cryptography;
using System.Text.Json;
using Agoda.IoC.Core;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Layouts;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The check of a custom layout (T10). The file is one JSON document { "markup": string, "css": string, "slots": object }
// with those three keys and no others. The scope of its compiled classes is the first 8 hex characters of the SHA-256
// of the bytes, which is also the start of the asset's id. A refusal is the single code layout_refused with every error
// the compiler found as a finding; a passing layout stores its compiled form only, never the uploaded markup or CSS,
// and is not approved.
[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class LayoutCheck(ICustomLayoutCompiler compiler) : IAssetCheck
{
    private const string MARKUP = "markup";
    private const string CSS = "css";
    private const string SLOTS = "slots";
    private const string CONTENT_TYPE = "application/json";

    public TemplateAssetKind Kind => TemplateAssetKind.Layout;

    public async Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var scope = Convert.ToHexStringLower(SHA256.HashData(bytes))[..LayoutScope.LENGTH];
        if (!TryParse(bytes, out var markup, out var css, out var slots))
        {
            return Refused(CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.LAYOUT_REFUSED));
        }

        var result = compiler.Compile(markup, css, slots, scope);
        if (result.Layout is null)
        {
            var errors = result.Errors.Count > 0
                ? result.Errors
                : [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.LAYOUT_REFUSED)];
            return Refused([.. errors]);
        }

        return AssetCheckResult.Accepted(CONTENT_TYPE, StoredLayout.New(result.Layout).Write());
    }

    private static AssetCheckResult Refused(params CustomLayoutError[] errors)
        => AssetCheckResult.Refused(TemplateErrorCodes.LAYOUT_REFUSED, [.. errors.Select(e => new TemplateRefusalDetail(e.Path, e.Code, e.Message))]);

    // Exactly the three keys, each once, of the right type. Anything else is not a layout upload.
    private static bool TryParse(byte[] bytes, out string markup, out string css, out string slots)
    {
        markup = css = slots = "";
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var properties = root.EnumerateObject().ToList();
            if (properties.Count != 3 || properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 3)
            {
                return false;
            }

            if (!root.TryGetProperty(MARKUP, out var markupJson) || markupJson.ValueKind != JsonValueKind.String
                || !root.TryGetProperty(CSS, out var cssJson) || cssJson.ValueKind != JsonValueKind.String
                || !root.TryGetProperty(SLOTS, out var slotsJson) || slotsJson.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            markup = markupJson.GetString()!;
            css = cssJson.GetString()!;
            slots = slotsJson.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
