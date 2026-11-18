using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// Every font file the manifest names has its rights confirmed by a person. A template with no fonts passes. Until a
// person confirms, the row is a todo that blocks publishing.
public sealed class FontRightsCheck : IPublishCheck
{
    private const string KEY = "fontRights";
    private const string LABEL = "Font rights confirmed";

    public CheckOutcome Run(PublishFacts facts)
    {
        var unconfirmed = facts.Manifest.Fonts.Any(font => facts.Asset(font.File) is not { Kind: TemplateAssetKind.Font, RightsConfirmed: true });
        return unconfirmed
            ? new CheckOutcome(
                CheckOutcome.Todo(KEY, LABEL, "Confirm you can serve these fonts.", "fonts").Row,
                true,
                TemplateErrorCodes.RIGHTS_NOT_CONFIRMED,
                "fonts",
                TemplateErrorTexts.Format(TemplateErrorCodes.RIGHTS_NOT_CONFIRMED))
            : CheckOutcome.Pass(KEY, LABEL, "", "fonts");
    }
}
