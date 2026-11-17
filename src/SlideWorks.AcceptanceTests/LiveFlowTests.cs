using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The owner goes live and ends from the plain player's live chip (SW-5 J2, J6, D18).
public class LiveFlowTests : AcceptanceTest
{
    [Test]
    public async Task GoLive_WhenOwnerClicks_ShouldShowCodeAndEndReturnsToGoLive()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync($"Live flow {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        await Expect(Page).ToHaveURLAsync(new Regex(@"/decks/[0-9A-Za-z]{12}$"));
        await Expect(deck.Present).ToBeVisibleAsync();
        await deck.Present.ClickAsync();
        var player = new PlayerPage(Page);
        await Expect(player.Player).ToBeVisibleAsync();

        await player.LiveChip.GoLive.ClickAsync();

        await Expect(player.LiveChip.Code).ToHaveTextAsync(new Regex(@"^\d{3} \d{3}$"));
        await Expect(player.LiveChip.Count).ToHaveTextAsync("0 joined");
        await Expect(player.LiveChip.Error).ToHaveCountAsync(0);

        await player.LiveChip.End.ClickAsync();

        await Expect(player.LiveChip.GoLive).ToBeVisibleAsync();
        await Expect(player.LiveChip.End).ToHaveCountAsync(0);
    }

    [Test]
    public async Task JoinBarToggle_WhenLive_ShouldDrawAndRemoveTheBarInTheSamePlayer()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync($"Join bar flow {Guid.NewGuid():N}");
        var deck = new DeckPage(Page);
        await Expect(deck.Present).ToBeVisibleAsync();
        await deck.Present.ClickAsync();
        var player = new PlayerPage(Page);
        await Expect(player.Player).ToBeVisibleAsync();
        await player.LiveChip.GoLive.ClickAsync();
        await Expect(player.LiveChip.End).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => { window.sameDocument = true; }");

        await player.LiveChip.JoinBarToggle.ClickAsync();

        await Expect(player.JoinBar(1)).ToBeVisibleAsync();
        await Expect(player.LiveChip.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "true");
        (await Page.EvaluateAsync<bool>("() => window.sameDocument === true")).ShouldBeTrue();

        await player.LiveChip.JoinBarToggle.ClickAsync();

        await Expect(player.JoinBar(1)).ToHaveCountAsync(0);
        await Expect(player.LiveChip.JoinBarToggle).ToHaveAttributeAsync("aria-pressed", "false");
    }
}
