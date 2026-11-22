using System.Text.Json;

namespace SlideWorks.AcceptanceTests.Pages;

// The template editor's Custom layouts tab: the draft's layouts with their status, and the upload of one layout's three
// files with the list of findings when they are refused.
public sealed class LayoutsTab(IPage page)
{
    private const string NAME_TEXT = "row => row.children[0].innerText";
    private const string FINDINGS = """
        list => JSON.stringify([...list.children].map(item => ({
          path: item.querySelector('code')?.textContent ?? null,
          message: item.querySelector('span')?.textContent ?? '',
        })))
        """;

    public ILocator Root => page.GetByTestId("template-tab-layouts");

    public ILocator Rows => page.GetByTestId("custom-layout-row");

    // The three files of one layout go into this input together.
    public ILocator Files => page.GetByTestId("layout-files");

    public ILocator Refusals => page.GetByTestId("layout-refusals");

    // The row of the layout with this name.
    public ILocator Row(string name) =>
        Rows.Filter(new LocatorFilterOptions { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(name)}(?=Checking|Needs review|Approved|Status unknown)") });

    // "Checking…", "Needs review" or "Approved".
    public ILocator StatusOf(string name) => Row(name).GetByTestId("custom-layout-status");

    // The names the tab shows: the text of the name in each visible row, in the order listed.
    public async Task<IReadOnlyList<string>> NamesAsync()
    {
        var names = new List<string>();
        foreach (var row in await Rows.AllAsync())
        {
            if (await row.IsVisibleAsync())
                names.Add(await row.EvaluateAsync<string>(NAME_TEXT));
        }
        return names;
    }

    // Each refusal finding: the path of the part it is about (null when it has none) and its message.
    public async Task<IReadOnlyList<Finding>> FindingsAsync() =>
        JsonSerializer.Deserialize<List<Finding>>(await Refusals.EvaluateAsync<string>(FINDINGS), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public sealed class Finding
    {
        public string? Path { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
