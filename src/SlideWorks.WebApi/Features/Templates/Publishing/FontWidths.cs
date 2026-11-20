using System.Globalization;
using System.Text.Json;
using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The glyph widths of a template's fonts: what a font file's MetaJson says, and the FitWidthsJson a published version
// stores from it.
public static class FontWidths
{
    // The width the built-in fonts are fitted with, kept for a role that has no font.
    public const double DEFAULT_EM = 0.56;

    // A font file's {"avgCharEm": 0.58}, or null when its meta holds no readable width.
    public static double? AvgCharEm(string? metaJson)
    {
        if (string.IsNullOrEmpty(metaJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metaJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("avgCharEm", out var width)
                && width.ValueKind == JsonValueKind.Number
                    ? width.GetDouble()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // {"body": em, "head": em, "cell": em}: body and cell are the widest body-role font, head the widest head-role font,
    // each 0.56 where the role has no font; decimals to two places.
    public static string Json(PublishFacts facts)
    {
        var body = Widest(facts, "body");
        var head = Widest(facts, "head");
        return $"{{\"body\":{Text(body)},\"head\":{Text(head)},\"cell\":{Text(body)}}}";
    }

    private static double Widest(PublishFacts facts, string role)
        => facts.Manifest.Fonts
            .Where(font => font.Role == role)
            .Select(font => AvgCharEm(facts.Asset(font.File)?.MetaJson))
            .OfType<double>()
            .DefaultIfEmpty(DEFAULT_EM)
            .Max();

    private static string Text(double em) => em.ToString("0.00", CultureInfo.InvariantCulture);
}
