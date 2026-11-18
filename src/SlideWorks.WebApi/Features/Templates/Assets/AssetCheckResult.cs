namespace SlideWorks.WebApi.Features.Templates.Assets;

// What a file check answers (T6). Accepted: Ok with the content type the file is served as and the JSON object stored
// as the asset's MetaJson (null when the kind has none): a font {"avgCharEm": 0.58}, a PNG or WebP
// {"width": px, "height": px}, an SVG none. Refused: the code (TemplateErrorCodes), the values for the placeholders of
// its text and the findings for the body's "errors", which is empty unless the check fills it.
public sealed record AssetCheckResult(
    bool Ok,
    string? ContentType,
    string? MetaJson,
    string? ErrorCode,
    IReadOnlyList<(string Name, string Value)> ErrorValues,
    IReadOnlyList<TemplateRefusalDetail> Errors)
{
    public static AssetCheckResult Accepted(string contentType, string? metaJson = null)
        => new(true, contentType, metaJson, null, [], []);

    public static AssetCheckResult Refused(string errorCode, params (string Name, string Value)[] errorValues)
        => new(false, null, null, errorCode, errorValues, []);

    public static AssetCheckResult Refused(
        string errorCode, IReadOnlyList<TemplateRefusalDetail> errors, params (string Name, string Value)[] errorValues)
        => new(false, null, null, errorCode, errorValues, errors);
}
