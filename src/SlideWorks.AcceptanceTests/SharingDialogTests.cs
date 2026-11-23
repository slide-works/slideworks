using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The sharing dialog against the real BFF: its PUT passes the cross-site write guard from the app's own origin, the
// server normalises the emails, and the deck page re-reads after Save. Each test makes its own deck.
public partial class SharingDialogTests : AcceptanceTest
{
    [Test]
    public async Task Share_WhenOwnerNamesAViewerAndSaves_ShouldUpdateTheDeckPageAndKeepTheNormalisedEmail()
    {
        await CreateDeckAsync(TestUser.Sam, $"Offsite {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);

        await deck.Share.ClickAsync();
        await Expect(dialog.Level("private")).ToBeCheckedAsync();
        await Expect(dialog.LastChange).ToHaveCountAsync(0);
        await dialog.ChooseAsync("named");
        await dialog.AddEmailAsync("  Viewer@Example.TEST ");
        await dialog.Save.ClickAsync();

        await Expect(dialog.Root).ToHaveCountAsync(0);
        await Expect(deck.Share).ToBeFocusedAsync();
        await Expect(deck.SharingLevel).ToHaveTextAsync("1 named viewer");

        await deck.Share.ClickAsync();
        await Expect(dialog.Level("named")).ToBeCheckedAsync();
        await Expect(dialog.PersonEmails).ToHaveTextAsync(["viewer@example.test"]);
        await Expect(dialog.LastChange).ToHaveTextAsync(new Regex("^Last change: .+, .+\\.$"));
    }

    [Test]
    public async Task Share_WhenPartialEmailTyped_ShouldAddNobody()
    {
        await CreateDeckAsync(TestUser.Sam, $"Review {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);

        await deck.Share.ClickAsync();
        await dialog.ChooseAsync("named");
        await dialog.AddEmailAsync("not-an-email");

        await Expect(dialog.EmailHint).ToHaveTextAsync("Type their full email to share this deck with them.");
        await Expect(dialog.Rows).ToHaveCountAsync(0);

        await dialog.AddEmailAsync("viewer@example.test");

        await Expect(dialog.EmailHint).ToHaveCountAsync(0);
        await Expect(dialog.Rows).ToHaveCountAsync(1);
    }

    // Signs in as presenter, makes a deck with "+ New deck" and waits for its deck page.
    private async Task CreateDeckAsync(TestUser presenter, string title)
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, presenter);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(new DeckPage(Page).Title).ToHaveTextAsync(title);
    }

    [GeneratedRegex(@"/decks/[0-9A-Za-z]{12}$")]
    private static partial Regex DeckPageUrl();
}
