namespace SlideWorks.AcceptanceTests.Pages;

public class LoginPage(IPage page)
{
    public ILocator TokenField => page.GetByTestId("token-input");

    public Task GoToAsync() => page.GotoAsync("/");

    public async Task EnterTokenAsync(string token)
    {
        await TokenField.FillAsync(token);
        await page.GetByTestId("token-submit").ClickAsync();
    }

    public async Task<NotesPage> SignInAsync(string token)
    {
        await EnterTokenAsync(token);
        var notes = new NotesPage(page);
        await notes.WaitUntilLoadedAsync();
        return notes;
    }
}
