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
        string? firstPath = null;
        var failed = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            string? path = null;
            if (facts.Asset(entry.File) is not { Kind: TemplateAssetKind.Layout, Layout: { } layout })
            {
                path = $"customLayouts[{i}].file";
            }
            else if (!string.Equals(layout.Name, entry.Name, StringComparison.Ordinal) || !seen.Add(entry.Name))
            {
                path = $"customLayouts[{i}].name";
            }

            if (path is not null)
            {
                firstPath ??= path;
                failed.Add(entry.Name);
            }
        }

        if (firstPath is not null)
        {
            return NotFound(firstPath, failed);
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

    // The refusal is the first problem's path; the row's detail names every affected layout.
    private static CheckOutcome NotFound(string path, IReadOnlyList<string> names)
    {
        var message = TemplateErrorTexts.Format(TemplateErrorCodes.LAYOUT_NOT_FOUND);
        return new CheckOutcome(
            new TemplateChecklistRow(KEY, LABEL, TemplateCheckState.Fail, $"{message}: {string.Join(", ", names.Distinct(StringComparer.Ordinal))}", TAB), true, TemplateErrorCodes.LAYOUT_NOT_FOUND, path, message);
    }
}
