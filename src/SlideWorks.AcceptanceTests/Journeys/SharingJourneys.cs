using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Sharing end to end in the browser: Sam makes a deck with "+ New deck", shares it from the deck page's sharing dialog,
// and someone else opens it: a guest through the link, or a named viewer through "Shared with you" in their library.
public partial class SharingJourneys : AcceptanceTest
{
    [Test]
    public async Task PresenterSharesDeckByLinkAndGuestOpensIt_WhenSharedByLink_ShouldOpenForAGuestSignedOut()
    {
        var title = $"Link Share {Guid.NewGuid():N}";
        var deckId = await CreateDeckAsync(title);
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);

        await deck.Share.ClickAsync();
        await dialog.ChooseAsync("link");
        await Expect(dialog.Link).ToHaveTextAsync(new Regex($"^{Regex.Escape(AcceptanceApp.BaseUrl)}/d/{deckId}/"));
        var link = await dialog.Link.TextContentAsync() ?? string.Empty;
        await dialog.Save.ClickAsync();
        await Expect(dialog.Root).ToHaveCountAsync(0);
        await Expect(deck.SharingLevel).ToHaveTextAsync("Anyone with the link");

        await using var guestContext = await Browser.NewContextAsync();
        var guest = await guestContext.NewPageAsync();
        var player = new PlayerPage(guest);
        await guest.GotoAsync(link);

        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Player).ToHaveAttributeAsync("aria-label", title);
        await Expect(player.NotFound).ToHaveCountAsync(0);
    }

    [Test]
    public async Task NamedViewerSeesDeckUnderSharedWithMeAndOpensIt_WhenNamed_ShouldListAndOpenItAndRefuseOthers()
    {
        var title = $"Named Share {Guid.NewGuid():N}";
        var deckId = await CreateDeckAsync(title);
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);

        await deck.Share.ClickAsync();
        await dialog.ChooseAsync("named");
        await dialog.AddEmailAsync(TestUser.Alex.Email);
        await dialog.Save.ClickAsync();
        await Expect(deck.SharingLevel).ToHaveTextAsync("1 named viewer");

        // Leave the deck page before swapping the session: a re-read it still has out (on join, or on the DeckChanged the
        // save publishes) could land after the swap and send the page away, aborting the next navigation.
        await Page.GotoAsync("about:blank");
        await Page.Context.ClearCookiesAsync();
        await SignIn.AsAsync(Page, TestUser.Alex);
        var decks = new DecksPage(Page);
        await decks.GotoAsync();
        var shared = decks.SharedRows.Filter(new LocatorFilterOptions { HasText = title });
        await Expect(shared).ToHaveCountAsync(1);
        await Expect(shared.GetByTestId("shared-owner")).ToHaveTextAsync(TestUser.Sam.Name);
        await Expect(decks.Row(title)).ToHaveCountAsync(0);
        await shared.GetByTestId("deck-title").ClickAsync();

        var player = new PlayerPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex($"/d/{deckId}/"));
        await Expect(player.Player).ToHaveAttributeAsync("aria-label", title);

        await Page.GotoAsync("about:blank");
        await Page.Context.ClearCookiesAsync();
        await SignIn.AsAsync(Page, TestUser.Mira);
        await player.GotoAsync(deckId);
        await Expect(player.NotFound).ToHaveTextAsync("Deck not found");
        await Expect(player.Player).ToHaveCountAsync(0);
    }

    // Signs in as Sam, makes a deck with "+ New deck" and returns its id once its deck page is open.
    private async Task<string> CreateDeckAsync(string title)
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(new DeckPage(Page).Title).ToHaveTextAsync(title);
        return DeckPageUrl().Match(Page.Url).Groups["id"].Value;
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
