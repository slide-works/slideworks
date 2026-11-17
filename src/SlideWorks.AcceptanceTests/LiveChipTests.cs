using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The owner's live chip in the plain player (SW-5 J6, D18, D19). It asserts only the chip's root test id.
public class LiveChipTests : AcceptanceTest
{
    [Test]
    public async Task Player_WhenOwner_ShouldMountLiveChip()
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        var title = $"Live chip {Guid.NewGuid():N}";
        await decks.CreateDeckAsync(title);
        var deck = new DeckPage(Page);
        await Expect(deck.Title).ToHaveTextAsync(title);
        await Expect(deck.Present).ToBeVisibleAsync();
        var player = new PlayerPage(Page);

        await deck.Present.ClickAsync();

        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.LiveChip.Root).ToBeVisibleAsync();
    }
}
