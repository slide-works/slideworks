using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Journeys.Support;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// Full email every time: after Sam shares one deck with Alex, a second deck starts with nobody, typing part of Alex's
// address suggests nobody, and only his full address adds him (as New).
public partial class ShareFullEmailJourney : AcceptanceTest
{
    private const string HINT = "Type their full email to share this deck with them.";

    [Test]
    public async Task Share_WhenSecondDeck_ShouldStartWithNobodyAndSuggestNobody()
    {
        var titleOne = $"First Deck {Guid.NewGuid():N}";
        var titleTwo = $"Second Deck {Guid.NewGuid():N}";
        var decks = new DecksPage(Page);
        var deck = new DeckPage(Page);
        var dialog = new SharingDialog(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(titleOne);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(deck.Title).ToHaveTextAsync(titleOne);

        await deck.Share.ClickAsync();
        await dialog.ChooseAsync("named");
        await dialog.AddEmailAsync(TestUser.Alex.Email);
        await Expect(dialog.Rows).ToHaveCountAsync(1);
        await dialog.Save.ClickAsync();
        await Expect(dialog.Root).ToHaveCountAsync(0);

        await decks.GotoAsync();
        await decks.CreateDeckAsync(titleTwo);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        await Expect(deck.Title).ToHaveTextAsync(titleTwo);
        await deck.Share.ClickAsync();
        await Expect(dialog.Owner).ToContainTextAsync(TestUser.Sam.Email);
        await Expect(dialog.Owner).ToBeVisibleAsync();
        await Expect(dialog.Rows).ToHaveCountAsync(0);

        await dialog.EmailInput.FillAsync("ale");
        await Expect(dialog.EmailHint).ToHaveTextAsync(HINT);
        await Expect(dialog.Root).Not.ToContainTextAsync(TestUser.Alex.Name);
        await Expect(dialog.Root).Not.ToContainTextAsync(TestUser.Alex.Email);
        await Expect(dialog.Rows).ToHaveCountAsync(0);
        await dialog.EmailInput.FillAsync("alex@team");
        await Expect(dialog.EmailHint).ToHaveTextAsync(HINT);
        await Expect(dialog.Root).Not.ToContainTextAsync(TestUser.Alex.Name);
        await Expect(dialog.Root).Not.ToContainTextAsync(TestUser.Alex.Email);
        await Expect(dialog.Rows).ToHaveCountAsync(0);

        await dialog.AddEmailAsync(TestUser.Alex.Email);
        await Expect(dialog.Rows).ToHaveCountAsync(1);
        await Expect(dialog.PersonEmails).ToHaveTextAsync(TestUser.Alex.Email);
        await Expect(dialog.NewTag(TestUser.Alex.Email)).ToBeVisibleAsync();
        await Expect(dialog.EmailHint).ToHaveCountAsync(0);
        await dialog.Cancel.ClickAsync();
        await Expect(dialog.Root).ToHaveCountAsync(0);

        await Sessions.SwitchSessionAsync(Page, TestUser.Alex);
        await decks.GotoAsync();
        await Expect(decks.SharedRow(titleOne)).ToBeVisibleAsync();
        await Expect(decks.SharedRow(titleOne)).ToHaveCountAsync(1);
        await Expect(decks.SharedRow(titleTwo)).ToHaveCountAsync(0);
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
