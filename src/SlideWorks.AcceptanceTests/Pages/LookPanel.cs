namespace SlideWorks.AcceptanceTests.Pages;

// The Look panel on the owner's deck page: template and palette selects, the chrome switches, the logo field and the
// draft switch. Each change is one PUT /api/decks/{id}/look; a template switch shows its warnings and a 400 its error.
public sealed class LookPanel(IPage page)
{
    public ILocator Root => page.GetByTestId("look-panel");

    public ILocator Template => page.GetByTestId("look-template");

    public ILocator Palette => page.GetByTestId("look-palette");

    public ILocator Warnings => page.GetByTestId("look-warning");

    // The update offer for a deck behind its template's latest version, its button, the "Now on version N." status that
    // follows the update (it takes focus) and the failure text.
    public ILocator VersionOffer => page.GetByTestId("look-version-offer");

    public ILocator VersionUpdate => page.GetByTestId("look-version-update");

    public ILocator VersionMessage => page.GetByTestId("look-version-message");

    public ILocator VersionError => page.GetByTestId("look-version-error");

    public ILocator Error => page.GetByTestId("look-error");

    public ILocator LogoFile => page.GetByTestId("logo-field-file");

    public ILocator LogoImage => page.GetByTestId("logo-field-image");

    public ILocator LogoError => page.GetByTestId("logo-field-error");

    // A template by its name in the catalogue, such as "update". The select is disabled until GET /api/looks loads.
    public async Task ChooseTemplateAsync(string name)
    {
        await Assertions.Expect(Template).ToBeEnabledAsync();
        await Template.SelectOptionAsync(name);
    }

    // A palette by its name in the catalogue, such as "midnight".
    public async Task ChoosePaletteAsync(string name)
    {
        await Assertions.Expect(Palette).ToBeEnabledAsync();
        await Palette.SelectOptionAsync(name);
    }

    // Picks a logo file, which the field uploads and then saves with the deck's look.
    public async Task UploadLogoAsync(string name, string mimeType, byte[] bytes) =>
        await LogoFile.SetInputFilesAsync(new FilePayload { Name = name, MimeType = mimeType, Buffer = bytes });
}
