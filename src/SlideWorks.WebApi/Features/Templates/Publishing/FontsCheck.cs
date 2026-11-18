using System.Globalization;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// Every font the manifest names is a font file of this template. A role with a regular weight and no bold one is a todo
// that does not block, as the checks screen shows.
public sealed class FontsCheck : IPublishCheck
{
    private const string KEY = "fonts";
    private const string LABEL = "Fonts fit slide text";
    private const int REGULAR = 400;
    private const int BOLD = 700;

    public CheckOutcome Run(PublishFacts facts)
    {
        var fonts = facts.Manifest.Fonts;
        for (var i = 0; i < fonts.Count; i++)
        {
            if (facts.Asset(fonts[i].File) is not { Kind: TemplateAssetKind.Font })
            {
                return CheckOutcome.Fail(
                    KEY, LABEL, "fonts", TemplateErrorCodes.ASSET_NOT_FOUND, $"fonts[{i}].file", TemplateErrorTexts.Format(TemplateErrorCodes.ASSET_NOT_FOUND));
            }
        }

        var missing = fonts
            .GroupBy(font => font.Role)
            .FirstOrDefault(role => role.Any(font => font.Weight == REGULAR) && !role.Any(font => font.Weight == BOLD));
        if (missing is not null)
        {
            return CheckOutcome.Todo(KEY, LABEL, $"The {missing.Key} font has a regular weight but no bold one.", "fonts");
        }

        return CheckOutcome.Pass(KEY, LABEL, Detail(facts), "fonts");
    }

    private static string Detail(PublishFacts facts)
    {
        var count = facts.Manifest.Fonts.Count;
        if (count == 0)
        {
            return "";
        }

        var widths = facts.Manifest.Fonts
            .Select(font => FontWidths.AvgCharEm(facts.Asset(font.File)?.MetaJson))
            .OfType<double>()
            .ToList();
        var files = count == 1 ? "1 font file" : $"{count} font files";
        return widths.Count == 0
            ? files + "."
            : string.Create(CultureInfo.InvariantCulture, $"{files}, all within {widths.Min():0.00} to {widths.Max():0.00} em.");
    }
}
