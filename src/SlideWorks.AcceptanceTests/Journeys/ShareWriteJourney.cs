using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Write access end to end: Sam names Alex with Can edit and Mira with Can view in the sharing dialog. Alex finds the deck
// in Shared with you as Can edit, edits its look on the deck page and Sam reads the change, with Alex's name, in the
// activity. Mira finds it as Can view and gets the player, with no Look panel.
public partial class ShareWriteJourney : AcceptanceTest
{
    [Test]
    public async Task Share_WhenCanEditAndCanView_ShouldLetOnlyAlexEdit()
    {
        var title = $"Write Share {Guid.NewGuid():N}";
        var decks = new DecksPage(Page);
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(deck.Title).ToHaveTextAsync(title);
        var deckId = DeckPageUrl().Match(Page.Url).Groups["id"].Value;

        // The owner sees Share and the Look panel; the people below must not.
        await Expect(deck.Share).ToBeVisibleAsync();
        await Expect(deck.LookPanel).ToBeVisibleAsync();
        await deck.Share.ClickAsync();
        await dialog.ChooseAsync("named");
        await dialog.AddEmailAsync(TestUser.Alex.Email);
        await dialog.SetAccessAsync(TestUser.Alex.Email, "write");
        await dialog.AddEmailAsync(TestUser.Mira.Email);
        await Expect(dialog.Access(TestUser.Alex.Email)).ToHaveValueAsync("write");
        await Expect(dialog.Access(TestUser.Mira.Email)).ToHaveValueAsync("read");
        await dialog.Save.ClickAsync();
        await Expect(dialog.Root).ToHaveCountAsync(0);

        // Leave the deck page before swapping the session, as SharingJourneys does.
        await Sessions.SwitchSessionAsync(Page, TestUser.Alex);
        await decks.GotoAsync();
        var alexRow = decks.SharedRow(title);
        await Expect(alexRow).ToHaveCountAsync(1);
        await Expect(DecksPage.SharedAccess(alexRow)).ToHaveTextAsync("Can edit");
        await alexRow.GetByTestId("deck-title").ClickAsync();
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(deck.Title).ToHaveTextAsync(title);
        await Expect(deck.AccessBadge).ToHaveTextAsync("Can edit");
        await Expect(deck.LookPanel).ToBeVisibleAsync();
        await Expect(deck.Share).ToHaveCountAsync(0);
        await deck.Look.ChoosePaletteAsync("midnight");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("palette");

        await Sessions.SwitchSessionAsync(Page, TestUser.Sam);
        await deck.GotoAsync(deckId);
        await Expect(deck.Share).ToBeVisibleAsync();
        await Expect(deck.ActivityRows.Filter(new LocatorFilterOptions { HasText = "palette" }).First).ToContainTextAsync(TestUser.Alex.Name);

        await Sessions.SwitchSessionAsync(Page, TestUser.Mira);
        await decks.GotoAsync();
        var miraRow = decks.SharedRow(title);
        await Expect(miraRow).ToHaveCountAsync(1);
        await Expect(DecksPage.SharedAccess(miraRow)).ToHaveTextAsync("Can view");
        await miraRow.GetByTestId("deck-title").ClickAsync();
        var player = new PlayerPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex($"/d/{deckId}/"));
        await Expect(player.Player).ToHaveAttributeAsync("aria-label", title);
        await Expect(deck.LookPanel).ToHaveCountAsync(0);
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
