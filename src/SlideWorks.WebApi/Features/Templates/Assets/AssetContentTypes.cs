namespace SlideWorks.WebApi.Features.Templates.Assets;

// The content types a template's file is ever served as. Anything else a check answers is served as UNKNOWN, which no
// browser draws or runs.
public static class AssetContentTypes
{
    public const string WOFF2 = "font/woff2";
    public const string SVG = "image/svg+xml";
    public const string PNG = "image/png";
    public const string WEBP = "image/webp";
    public const string UNKNOWN = "application/octet-stream";

    private static readonly string[] SERVED = [WOFF2, SVG, PNG, WEBP];

    public static string Served(string stored) => SERVED.Contains(stored, StringComparer.Ordinal) ? stored : UNKNOWN;
}
