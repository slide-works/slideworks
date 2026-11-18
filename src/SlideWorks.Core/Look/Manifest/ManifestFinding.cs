namespace SlideWorks.Core.Look.Manifest;

// One thing a manifest check refuses: a code of template-errors.json, where in the manifest (such as
// palette.extras[1].ink, or fonts) and the code's text with its placeholders filled.
public sealed record ManifestFinding(string Code, string Path, string Message)
{
    public const string MANIFEST_INVALID = "manifest_invalid";
    public const string PALETTE_CONTRAST = "palette_contrast";

    public static ManifestFinding Of(string code, string path) => new(code, path, TemplateErrorTexts.Format(code));

    public static ManifestFinding Invalid(string path, string reason) =>
        new(MANIFEST_INVALID, path, TemplateErrorTexts.Format(MANIFEST_INVALID, ("reason", $"{path} {reason}")));

    // A pair below its bar. The ratio is cut, not rounded, to one decimal place, so a pair that fails never reads as
    // the bar it missed (4.46 is 4.4:1, not 4.5:1).
    public static ManifestFinding Contrast(string path, string pair, double ratio, double needed) =>
        new(PALETTE_CONTRAST, path, TemplateErrorTexts.Format(
            PALETTE_CONTRAST,
            ("pair", pair),
            ("needed", RatioText(needed)),
            ("ratio", RatioText(Math.Floor(ratio * 10) / 10))));

    private static string RatioText(double ratio) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{ratio:0.0}:1");
}
