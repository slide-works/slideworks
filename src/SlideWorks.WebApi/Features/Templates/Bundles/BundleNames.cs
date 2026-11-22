using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Bundles;

// The names a bundle may hold. A path inside a bundle is a relative path of plain segments, and no name from a bundle
// is ever turned into a path on disk: the service never writes anything under an entry's name.
public static class BundleNames
{
    public const string MANIFEST = "template.json";

    private const string FALLBACK_FILE_NAME = "template";
    private const int MAX_FILE_NAME = 60;

    // True for a relative path of plain segments: at most MAX_FILE characters, no empty, "." or ".." segment (so no
    // leading "/"), no backslash, no colon (a drive letter) and no control character. A folder entry's trailing "/" is
    // the caller's to remove first.
    public static bool IsPlainPath(string path)
    {
        if (path.Length is 0 or > ManifestJson.MAX_FILE)
        {
            return false;
        }
        if (path.Any(c => char.IsControl(c) || c is '\\' or ':'))
        {
            return false;
        }
        return path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));
    }

    // The name a bundle is downloaded under: the template's short name with every character that is not a letter, a
    // digit, a hyphen or an underscore made a hyphen, trimmed to 60 characters, "template" when no letter or digit is
    // left, and always ending in .zip.
    public static string DownloadName(string shortName)
    {
        var kept = new string([.. shortName.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-')]);
        var trimmed = kept.Length > MAX_FILE_NAME ? kept[..MAX_FILE_NAME] : kept;
        var name = trimmed.Trim('-');
        return (name.Any(char.IsAsciiLetterOrDigit) ? name : FALLBACK_FILE_NAME) + ".zip";
    }
}
