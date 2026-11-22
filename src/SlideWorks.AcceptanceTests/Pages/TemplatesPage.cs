using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Pages;

// The templates library at /templates: the person's own templates, "Shared with you" and the built-ins in one table, with
// "New template" and its dialog. Also the public page at /t/{id}, which anyone the template reaches can open.
public sealed class TemplatesPage(IPage page)
{
    public const string PATH = "/templates";

    public ILocator Root => page.GetByTestId("templates-page");

    public ILocator NewTemplate => page.GetByTestId("new-template");

    public ILocator NewDialog => page.GetByTestId("new-template-dialog");

    public ILocator NewName => page.GetByTestId("new-template-name");

    public ILocator NewCreate => page.GetByTestId("new-template-create");

    // A group of rows by its kind: "mine", "shared" or "builtIn".
    public ILocator Group(string kind) => page.GetByTestId($"template-group-{kind}");

    // The public page at /t/{id}: its heading is the template's name, or the message when it is private or unknown.
    public ILocator PublicPage => page.GetByTestId("template-public-page");

    public ILocator PublicName => page.GetByTestId("template-public-name");

    public ILocator PublicMessage => page.GetByTestId("template-public-message");

    public ILocator PublicUse => page.GetByTestId("template-public-use");

    public static string PublicPath(string templateId) => $"/t/{templateId}";

    // The row in a group whose name is exactly name.
    public ILocator Row(string kind, string name) =>
        Group(kind).GetByTestId("template-row").Filter(new LocatorFilterOptions
        {
            Has = page.GetByTestId("template-name").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") }),
        });

    public async Task GotoAsync() => await page.GotoAsync(PATH);

    // "New template": a name, the built-in to start from, Create draft. The page moves to the new draft's editor.
    public async Task CreateAsync(string name, string builtIn)
    {
        await NewTemplate.ClickAsync();
        await Expect(NewDialog).ToBeVisibleAsync();
        await NewName.FillAsync(name);
        await page.GetByTestId($"new-template-from-{builtIn}").CheckAsync();
        await NewCreate.ClickAsync();
    }
}
