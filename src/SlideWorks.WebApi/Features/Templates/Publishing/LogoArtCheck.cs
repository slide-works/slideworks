using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// Every logo and art reference is a file of this template, of the kind that field takes.
public sealed class LogoArtCheck : IPublishCheck
{
    private const string KEY = "logoArt";
    private const string LABEL = "Logo and art are safe to serve";

    public CheckOutcome Run(PublishFacts facts)
    {
        var wrong = ManifestFiles.Refs(facts.Manifest)
            .Where(file => file.Kind is ManifestFiles.LOGO or ManifestFiles.ART)
            .FirstOrDefault(file => facts.Asset(file.Value) is not { } asset || asset.Kind != Expected(file.Kind));
        return wrong is null
            ? CheckOutcome.Pass(KEY, LABEL, "", "logo-art")
            : CheckOutcome.Fail(
                KEY, LABEL, "logo-art", TemplateErrorCodes.ASSET_NOT_FOUND, wrong.Path, TemplateErrorTexts.Format(TemplateErrorCodes.ASSET_NOT_FOUND));
    }

    private static TemplateAssetKind Expected(string fileKind) => fileKind == ManifestFiles.LOGO ? TemplateAssetKind.Logo : TemplateAssetKind.Art;
}
