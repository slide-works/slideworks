namespace SlideWorks.AcceptanceTests.Pages;

// The sharing dialog the deck page's Share button opens: private, anyone with the link, or named people as email chips,
// the deck's link with Copy link, Cancel and Save. Save closes the dialog and the deck page re-reads.
public sealed class SharingDialog(IPage page)
{
    public ILocator Root => page.GetByTestId("sharing-dialog");

    public ILocator Chips => Root.GetByTestId("sharing-chip-email");

    public ILocator EmailInput => Root.GetByTestId("sharing-email-input");

    public ILocator Error => Root.GetByTestId("sharing-error");

    public ILocator LastChange => Root.GetByTestId("sharing-last-change");

    public ILocator Link => Root.GetByTestId("sharing-link");

    public ILocator CopyLink => Root.GetByTestId("sharing-copy-link");

    public ILocator Save => Root.GetByTestId("sharing-save");

    public ILocator Cancel => Root.GetByTestId("sharing-cancel");

    // The radio for a level by its wire name: "private", "link" or "named".
    public ILocator Level(string level) => Root.GetByTestId($"sharing-level-{level}").GetByTestId("sharing-radio");

    public async Task ChooseAsync(string level) => await Level(level).CheckAsync();

    // Types an email and presses Enter, which turns it into a chip.
    public async Task AddEmailAsync(string email)
    {
        await EmailInput.FillAsync(email);
        await EmailInput.PressAsync("Enter");
    }

    public async Task RemoveEmailAsync(string email) =>
        await Root.GetByTestId("sharing-chip").Filter(new LocatorFilterOptions { HasText = email }).GetByTestId("sharing-chip-remove").ClickAsync();
}
