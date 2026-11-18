using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// The editor view's checklist in the tools' terms.
public static class TemplateChecks
{
    public static IReadOnlyList<TemplateCheck> Of(TemplateEditorView view)
        => [.. view.Checklist.Select(row => new TemplateCheck(row.Key, row.State == TemplateCheckState.Pass, row.Detail))];
}
