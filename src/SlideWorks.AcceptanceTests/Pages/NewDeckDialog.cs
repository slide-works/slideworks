namespace SlideWorks.AcceptanceTests.Pages;

// The New deck dialog that "+ New deck" opens on the decks library: a title, a template, a palette, a live preview of
// two sample slides, and "Create deck", which moves to the new deck's page.
public sealed class NewDeckDialog(IPage page)
{
    public ILocator Root => page.GetByTestId("new-deck-dialog");

    public ILocator Title => page.GetByTestId("new-deck-title");

    public ILocator Palette => page.GetByTestId("new-deck-palette");

    public ILocator Preview => page.GetByTestId("look-preview");

    public ILocator Create => page.GetByTestId("new-deck-create");

    public ILocator Cancel => page.GetByTestId("new-deck-cancel");

    public ILocator Error => page.GetByTestId("new-deck-error");

    // A template option by its name in the catalogue, such as "update".
    public ILocator Template(string name) => page.GetByTestId($"new-deck-template-{name}");

    public async Task FillTitleAsync(string title) => await Title.FillAsync(title);

    public async Task ChooseTemplateAsync(string name) => await Template(name).CheckAsync();

    public async Task ChoosePaletteAsync(string name) => await Palette.SelectOptionAsync(name);

    public async Task CreateAsync() => await Create.ClickAsync();
}
