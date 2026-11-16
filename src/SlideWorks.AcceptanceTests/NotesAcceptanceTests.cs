using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using SlideWorks.AcceptanceTests.Pages;

namespace SlideWorks.AcceptanceTests;

[TestFixture]
public class NotesAcceptanceTests : AcceptanceTest
{
    [Test]
    public async Task AddNote_WhenTextEntered_ShowsItInTheListAndKeepsItAfterReload()
    {
        var notes = await OpenNotesPageAsync();
        var text = $"Acceptance note {Guid.NewGuid():N}";

        await notes.AddAsync(text);
        await Expect(notes.ItemsWithText(text)).ToHaveCountAsync(1);

        await Page.ReloadAsync();
        await Expect(notes.ItemsWithText(text)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task DeleteNote_WhenDeleteClicked_RemovesItFromTheList()
    {
        var notes = await OpenNotesPageAsync();
        var text = $"Doomed note {Guid.NewGuid():N}";
        await notes.AddAsync(text);
        await Expect(notes.ItemsWithText(text)).ToHaveCountAsync(1);

        await notes.DeleteAsync(text);

        await Expect(notes.ItemsWithText(text)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SignIn_WhenTokenIsWrong_ReturnsToTheTokenScreen()
    {
        var login = new LoginPage(Page);
        await login.GoToAsync();

        await login.EnterTokenAsync("not-the-token");

        // The first API call answers 401, which sends the SPA back to the token screen.
        await Expect(login.TokenField).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/login$"));
    }
}
