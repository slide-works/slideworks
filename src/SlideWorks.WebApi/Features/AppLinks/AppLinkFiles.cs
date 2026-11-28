using Agoda.IoC.Core;
namespace SlideWorks.WebApi.Features.AppLinks;

// Builds the two app-link files from configuration, read when asked. A file whose setting is unset (or, for Android, invalid)
// null (or, for the list, empty), which the controller turns into a 404. A bad Android fingerprint is logged once, never thrown.
[RegisterSingleton]
public class AppLinkFiles(IConfiguration configuration, ILogger<AppLinkFiles> logger)
{
    private int _warned;

    public AppleAppSiteAssociationView? AppleAppSiteAssociation(bool onJoinHost)
        => AppLinkSettings.AppleAppId(configuration) is { } appId ? AppleAppSiteAssociationView.From(appId, onJoinHost) : null;

    public IReadOnlyList<AssetLinkView> AssetLinks()
    {
        WarnOnceWhenInvalid();
        return AppLinkSettings.AndroidFingerprints(configuration) is { Count: > 0 } fingerprints
            ? [AssetLinkView.From(AppLinkSettings.ANDROID_PACKAGE, fingerprints)]
            : [];
    }

    private void WarnOnceWhenInvalid()
    {
        if (AppLinkSettings.AndroidFingerprintsAreInvalid(configuration) && Interlocked.Exchange(ref _warned, 1) == 0)
        {
            logger.LogWarning("{Setting} has an entry that is not 32 colon-separated hex bytes; assetlinks.json is not served",
                AppLinkSettings.ANDROID_CERT_SHA256_KEY);
        }
    }
}
