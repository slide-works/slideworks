using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// Every custom layout of the manifest is a layout file of this template, under the name it was compiled as and listed
// once, and every one has been approved by the owner. The count is LimitsCheck's. The first problem found is the refusal.
public sealed class LayoutsCheck : IPublishCheck
{
    private const string KEY = "layouts";
    private const string LABEL = "Custom layouts are approved";
    private const string TAB = "layouts";

    public CheckOutcome Run(PublishFacts facts)
    {
        var entries = facts.Manifest.CustomLayouts;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (facts.Asset(entry.File) is not { Kind: TemplateAssetKind.Layout, Layout: { } layout })
            {
                return NotFound($"customLayouts[{i}].file");
            }

            if (!string.Equals(layout.Name, entry.Name, StringComparison.Ordinal) || !seen.Add(entry.Name))
            {
                return NotFound($"customLayouts[{i}].name");
            }
        }

        var unapproved = entries.Where(entry => !facts.Asset(entry.File)!.Layout!.Approved).Select(entry => entry.Name).ToList();
        if (unapproved.Count > 0)
        {
            var code = TemplateErrorCodes.LAYOUT_NOT_APPROVED;
            var message = TemplateErrorTexts.Format(code);
            return new CheckOutcome(
                new TemplateChecklistRow(KEY, LABEL, TemplateCheckState.Fail, string.Join(", ", unapproved), TAB), true, code, unapproved[0], message);
        }

        return CheckOutcome.Pass(KEY, LABEL, entries.Count == 0 ? "" : $"{entries.Count} approved", TAB);
    }

    private static CheckOutcome NotFound(string path)
        => CheckOutcome.Fail(KEY, LABEL, TAB, TemplateErrorCodes.LAYOUT_NOT_FOUND, path, TemplateErrorTexts.Format(TemplateErrorCodes.LAYOUT_NOT_FOUND));
}
