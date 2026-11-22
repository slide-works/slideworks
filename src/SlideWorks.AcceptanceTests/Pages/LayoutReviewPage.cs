using System.Text.Json;

namespace SlideWorks.AcceptanceTests.Pages;

// The layout review page at /templates/{id}/layouts/{name}: the status, Approve, what the page's own measurement found,
// the checks list and the renders with the dashed safe-area box.
public sealed class LayoutReviewPage(IPage page)
{
    private const string CHECKS = """
        list => JSON.stringify(Object.fromEntries([...list.children].map(item => [item.children[0].textContent, item.children[1].textContent])))
        """;

    private const string SAFE_AREA = """
        el => {
          const style = getComputedStyle(el);
          const left = parseFloat(style.left), top = parseFloat(style.top);
          return JSON.stringify({ left, top, right: left + parseFloat(style.width), bottom: top + parseFloat(style.height) });
        }
        """;

    public ILocator Root => page.GetByTestId("layout-review-page");

    // "Needs review" or "Approved"; focus moves here after approving.
    public ILocator Status => page.GetByTestId("layout-review-status");

    public ILocator Approve => page.GetByTestId("layout-review-approve");

    // What the page found, listed only while there is something wrong; empty otherwise.
    public ILocator Reason => page.GetByTestId("layout-review-approve-reason");

    public ILocator Checks => page.GetByTestId("layout-review-checks");

    // One render by its kind: "sample", "dark", "steps" or "maximum".
    public ILocator Render(string kind) => page.GetByTestId($"layout-review-render-{kind}");

    public ILocator SafeArea => page.GetByTestId("layout-review-safe-area");

    public static string PathFor(string templateId, string name) => $"/templates/{templateId}/layouts/{name}";

    // Each check's label and its word ("Passed", "Failed" or "Not yet").
    public async Task<Dictionary<string, string>> ChecksAsync() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(await Checks.EvaluateAsync<string>(CHECKS))!;

    // The dashed box's edges in the slide's own pixels.
    public async Task<Box> SafeAreaBoxAsync() => JsonSerializer.Deserialize<Box>(await SafeArea.First.EvaluateAsync<string>(SAFE_AREA), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    public sealed class Box
    {
        public double Left { get; init; }

        public double Top { get; init; }

        public double Right { get; init; }

        public double Bottom { get; init; }
    }
}
