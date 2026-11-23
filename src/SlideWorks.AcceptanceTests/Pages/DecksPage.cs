using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The decks library at /decks, inside the signed-in shell: the presenter's own decks, "Shared with you", the "Connect
// an agent" banner and "+ New deck".
public sealed class DecksPage(IPage page)
{
    public const string PATH = "/decks";

    public ILocator Shell => page.GetByTestId("app-shell");

    public ILocator UserMenu => page.GetByTestId("user-menu");

    public ILocator Library => page.GetByTestId("decks-page");

    public ILocator Error => page.GetByTestId("decks-error");

    public ILocator ConnectBanner => page.GetByTestId("connect-banner");

    public ILocator McpUrl => ConnectBanner.GetByTestId("connect-mcp-url");

    public ILocator Rows => page.GetByTestId("decks-table").GetByTestId("deck-row");

    public ILocator SharedRows => page.GetByTestId("shared-with-me").GetByTestId("shared-row");

    // A "Shared with you" row by its exact title, and its "Can edit" or "Can view" cell.
    public ILocator SharedRow(string title) =>
        SharedRows.Filter(new LocatorFilterOptions
        {
            Has = page.GetByTestId("deck-title").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(title)}$") }),
        });

    public static ILocator SharedTitle(ILocator row) => row.GetByTestId("deck-title");

    public static ILocator SharedAccess(ILocator row) => row.GetByTestId("shared-access");

    public ILocator Empty => page.GetByTestId("decks-empty");

    // One of the presenter's own decks, by its exact title.
    public ILocator Row(string title) =>
        Rows.Filter(new LocatorFilterOptions
        {
            Has = page.GetByTestId("deck-title").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(title)}$") }),
        });

    public static ILocator LastChange(ILocator row) => row.GetByTestId("deck-last-change");

    public static ILocator Sharing(ILocator row) => row.GetByTestId("deck-sharing");

    public static ILocator Present(ILocator row) => row.GetByTestId("deck-present");

    public async Task GotoAsync() => await page.GotoAsync(PATH);

    public ILocator NewDeck => page.GetByTestId("new-deck");

    // "+ New deck": opens the New deck dialog.
    public async Task<NewDeckDialog> OpenNewDeckAsync()
    {
        await NewDeck.ClickAsync();
        var dialog = new NewDeckDialog(page);
        await Expect(dialog.Root).ToBeVisibleAsync();
        return dialog;
    }

    // "+ New deck" with the default template and palette: types the title and creates the deck. The page then moves to
    // the new deck's page.
    public async Task CreateDeckAsync(string title)
    {
        var dialog = await OpenNewDeckAsync();
        await dialog.FillTitleAsync(title);
        await dialog.CreateAsync();
    }
}
