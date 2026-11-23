namespace SlideWorks.AcceptanceTests.Pages;

// The sharing dialog the deck page's Share button opens: private, anyone with the link or only people below, a People
// section (the owner's row, then one row per person with Can view or Can edit and Remove), the deck's link with Copy
// link, Cancel and Save. Save closes the dialog and the deck page re-reads.
public sealed class SharingDialog(IPage page)
{
    public ILocator Root => page.GetByTestId("sharing-dialog");

    // The email of each person row (not the owner's).
    public ILocator Chips => Root.GetByTestId("sharing-person-email");

    public ILocator Rows => Root.GetByTestId("sharing-person");

    public ILocator Owner => Root.GetByTestId("sharing-owner");

    public ILocator EmailInput => Root.GetByTestId("sharing-email-input");

    public ILocator EmailHint => Root.GetByTestId("sharing-email-hint");

    public ILocator Error => Root.GetByTestId("sharing-error");

    public ILocator LimitError => Root.GetByTestId("sharing-limit-error");

    public ILocator NewNote => Root.GetByTestId("sharing-new-note");

    public ILocator LastChange => Root.GetByTestId("sharing-last-change");

    public ILocator Link => Root.GetByTestId("sharing-link");

    public ILocator CopyLink => Root.GetByTestId("sharing-copy-link");

    public ILocator Save => Root.GetByTestId("sharing-save");

    public ILocator Cancel => Root.GetByTestId("sharing-cancel");

    // The radio for a level by its wire name: "private", "link" or "named".
    public ILocator Level(string level) => Root.GetByTestId($"sharing-level-{level}").GetByTestId("sharing-radio");

    public async Task ChooseAsync(string level) => await Level(level).CheckAsync();

    private ILocator Row(string email) => Rows.Filter(new LocatorFilterOptions { HasText = email });

    // The "Can view" or "Can edit" select of a person's row, and its "paused" tag.
    public ILocator Access(string email) => Row(email).GetByTestId("sharing-person-access");

    public ILocator Paused(string email) => Row(email).GetByTestId("sharing-person-paused");

    // Types a full email and presses Enter, which adds the person as Can view.
    public async Task AddEmailAsync(string email)
    {
        await EmailInput.FillAsync(email);
        await EmailInput.PressAsync("Enter");
    }

    // Picks "read" or "write" for a person already on the list.
    public async Task SetAccessAsync(string email, string access) =>
        await Access(email).SelectOptionAsync(access);

    public async Task RemoveEmailAsync(string email) =>
        await Row(email).GetByTestId("sharing-person-remove").ClickAsync();
}
