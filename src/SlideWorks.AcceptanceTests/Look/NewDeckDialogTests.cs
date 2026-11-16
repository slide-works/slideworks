using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Look;

// The New deck dialog against the real BFF: the template and palette come from GET /api/looks, and "Create deck"
// posts them with the title, so the new deck starts with that look. Titles are unique per test, because the
// acceptance database is shared by the whole run.
public partial class NewDeckDialogTests : AcceptanceTest
{
    [Test]
    public async Task PersonCreatesDeckWithTemplateAndPalette_WhenUpdateAndPlumChosen_ShouldLandOnItsDeckPage()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();

        var dialog = await decks.OpenNewDeckAsync();
        await dialog.FillTitleAsync($"Monthly update {Guid.NewGuid():N}");
        await dialog.ChooseTemplateAsync("update");
        await dialog.ChoosePaletteAsync("plum");
        await Expect(dialog.Preview).ToHaveAttributeAsync("data-template", "update");
        await Expect(dialog.Preview).ToHaveAttributeAsync("data-palette", "plum");
        await dialog.CreateAsync();

        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        var deck = new DeckPage(Page);
        await Expect(deck.ActivityRows.First).ToContainTextAsync("You created the deck from the update template with the Plum palette");
    }

    [GeneratedRegex(@"/decks/[0-9A-Za-z]{12}$")]
    private static partial Regex DeckPageUrl();
}
