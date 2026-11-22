namespace SlideWorks.AcceptanceTests.Pages;

// The template's sharing dialog from the editor's Share button. It saves on each change and Done closes it; unlike the
// deck's dialog it has no Save.
public sealed class TemplateShareDialog(IPage page)
{
    public ILocator Root => page.GetByTestId("template-share-dialog");

    public ILocator Chips => Root.GetByTestId("sharing-chip-email");

    public ILocator EmailInput => Root.GetByTestId("sharing-email-input");

    public ILocator Done => Root.GetByTestId("template-share-done");

    public ILocator Error => Root.GetByTestId("template-share-error");

    // The radio for a level by its wire name: "private", "link" or "named".
    public ILocator Level(string level) => Root.GetByTestId($"sharing-level-{level}").GetByTestId("sharing-radio");

    public async Task ChooseAsync(string level) => await Level(level).CheckAsync();

    // Types an email and presses Enter, which turns it into a chip and saves.
    public async Task AddEmailAsync(string email)
    {
        await EmailInput.FillAsync(email);
        await EmailInput.PressAsync("Enter");
    }
}
