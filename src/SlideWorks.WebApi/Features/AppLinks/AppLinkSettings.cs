using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Features.AppLinks;

// The app-link settings and the one fixed package the phone app is published under. Both values are read when asked, so an
// empty or unset one simply means the file is not served.
public static class AppLinkSettings
{
    public const string APPLE_APP_ID_KEY = "SLIDEWORKS_APPLE_APP_ID";
    public const string ANDROID_CERT_SHA256_KEY = "SLIDEWORKS_ANDROID_CERT_SHA256";
    public const string ANDROID_PACKAGE = "app.slideworks.mobile";

    public static string? AppleAppId(IConfiguration configuration) => NullIfBlank(configuration[APPLE_APP_ID_KEY]);

    // The listed fingerprints, upper-cased (Google's format), or an empty list when any one is not 32 colon-separated hex bytes:
    // one bad entry makes the whole setting count as not configured.
    public static IReadOnlyList<string> AndroidFingerprints(IConfiguration configuration)
        => AndroidFingerprintsAreInvalid(configuration) ? [] : RawFingerprints(configuration).Select(f => f.ToUpperInvariant()).ToList();

    public static bool AndroidFingerprintsAreInvalid(IConfiguration configuration)
        => RawFingerprints(configuration).Any(f => !FINGERPRINT.IsMatch(f));

    private static readonly Regex FINGERPRINT = new("^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){31}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static string[] RawFingerprints(IConfiguration configuration)
        => (configuration[ANDROID_CERT_SHA256_KEY] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
