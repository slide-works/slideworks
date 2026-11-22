namespace SlideWorks.AcceptanceTests.Pages;

// The template editor at /templates/{id}/edit: the status chip, the tab bar, the Fonts tab and the publish checklist
// with its Publish button, and Share.
public sealed class TemplateEditorPage(IPage page)
{
    public ILocator Root => page.GetByTestId("template-editor");

    // "Draft", or "Published · v1" once published.
    public ILocator Status => page.GetByTestId("template-editor-status");

    public ILocator Share => page.GetByTestId("template-editor-share");

    public ILocator Tab(string tab) => page.GetByTestId($"template-editor-tab-{tab}");

    public ILocator FontsTab => page.GetByTestId("template-tab-fonts");

    // The heading and body family fields of the Fonts tab, by field: "head" or "body".
    public ILocator Family(string field) => page.GetByTestId($"font-type-{field}");

    public ILocator RightsTick => page.GetByTestId("font-rights-tick");

    public ILocator AddFiles => page.GetByTestId("font-add-files");

    public ILocator FileInput => page.GetByTestId("font-file-input");

    // The stored files not in the manifest yet, each with a role, family, weight and "Add to template".
    public ILocator ToAdd => page.GetByTestId("font-to-add");

    public ILocator ToAddFamily => page.GetByTestId("font-to-add-family");

    public ILocator ToAddWeight => page.GetByTestId("font-to-add-weight");

    public ILocator ToAddRole => page.GetByTestId("font-to-add-role");

    public ILocator AddToTemplate => page.GetByTestId("font-to-add-add");

    public ILocator Publish => page.GetByTestId("template-publish");

    public ILocator ChecklistCount => page.GetByTestId("checklist-count");

    public static string PathFor(string templateId) => $"/templates/{templateId}/edit";

    // Every checklist row's state by its key ("pass", "fail" or "todo"), read from the whole checklist.
    public async Task<Dictionary<string, string>> ChecklistAsync()
    {
        var states = new Dictionary<string, string>();
        foreach (var row in await page.GetByTestId("checklist-row").AllAsync())
            states[await row.GetAttributeAsync("data-key") ?? string.Empty] = await row.GetAttributeAsync("data-state") ?? string.Empty;
        return states;
    }
}
