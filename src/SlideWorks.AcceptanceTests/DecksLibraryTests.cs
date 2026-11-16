using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The decks library against the real BFF: "+ New deck" posts from the app's own origin, so the cross-site write guard
// lets it through, and the library lists what the presenter made. Titles are unique per test, because the acceptance
// database is shared by the whole run.
public partial class DecksLibraryTests : AcceptanceTest
{
    [Test]
    public async Task NewDeck_WhenPresenterCreatesOne_ShouldOpenItsDeckPage()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();

        await decks.CreateDeckAsync($"Team retro {Guid.NewGuid():N}");

        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
    }

    // red until SW-28: GET /api/decks is A1's, and the library shows an error until it lands.
    [Test]
    public async Task Library_WhenPresenterCreatedDeck_ShouldListItWithYouAsLastChange()
    {
        var decks = new DecksPage(Page);
        var title = $"Quarterly plan {Guid.NewGuid():N}";
        await SignIn.AsAsync(Page, TestUser.Mira);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());

        await decks.GotoAsync();

        var row = decks.Row(title);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(DecksPage.LastChange(row)).ToHaveTextAsync("You · Created from the talk template with the Cobalt palette");
        await Expect(DecksPage.Sharing(row)).ToHaveTextAsync("Private");
        await Expect(DecksPage.Present(row)).ToHaveAttributeAsync("href", PlayUrl());
        await Expect(decks.Error).ToHaveCountAsync(0);
    }

    [GeneratedRegex(@"/decks/[0-9A-Za-z]{12}$")]
    private static partial Regex DeckPageUrl();

    [GeneratedRegex(@"^/d/[0-9A-Za-z]{12}/quarterly-plan-[0-9a-f]{32}$")]
    private static partial Regex PlayUrl();
}
