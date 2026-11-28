namespace SlideWorks.WebApi.Features.AppLinks;

// The app-link settings and the one fixed package the phone app is published under. Both values are read when asked, so an
// empty or unset one simply means the file is not served.
public static class AppLinkSettings
{
    public const string APPLE_APP_ID_KEY = "SLIDEWORKS_APPLE_APP_ID";
    public const string ANDROID_CERT_SHA256_KEY = "SLIDEWORKS_ANDROID_CERT_SHA256";
    public const string ANDROID_PACKAGE = "app.slideworks.mobile";

    public static string? AppleAppId(IConfiguration configuration) => NullIfBlank(configuration[APPLE_APP_ID_KEY]);

    public static IReadOnlyList<string> AndroidFingerprints(IConfiguration configuration)
        => (configuration[ANDROID_CERT_SHA256_KEY] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
