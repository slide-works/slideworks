namespace SlideWorks.AcceptanceTests.Pages;

public class NotesPage(IPage page)
{
    public ILocator Heading => page.GetByTestId("notes-heading");

    public Task GoToAsync() => page.GotoAsync("/");

    public Task WaitUntilLoadedAsync() => Heading.WaitForAsync();

    // The note rows whose text contains the given text.
    public ILocator ItemsWithText(string text) => page.GetByTestId("note-item").Filter(new() { HasText = text });

    public async Task AddAsync(string text)
    {
        await page.GetByTestId("note-input").FillAsync(text);
        await page.GetByTestId("note-add").ClickAsync();
    }

    public Task DeleteAsync(string text) => ItemsWithText(text).GetByTestId("note-delete").ClickAsync();
}
