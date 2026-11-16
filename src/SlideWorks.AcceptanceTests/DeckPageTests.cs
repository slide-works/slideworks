using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The deck page and the player page against the real BFF. Each test makes its own deck through "+ New deck", because
// the acceptance database is shared by the whole run.
public partial class DeckPageTests : AcceptanceTest
{
    [Test]
    public async Task DeckPage_WhenPresenterCreatedDeck_ShouldShowItAndItsCreation()
    {
        var title = $"Roadmap {Guid.NewGuid():N}";
        var id = await CreateDeckAsync(TestUser.Sam, title);
        var deck = new DeckPage(Page);

        await Expect(deck.Title).ToHaveTextAsync(title);
        await Expect(deck.SlideCount).ToHaveTextAsync("0 slides");
        await Expect(deck.SharingLevel).ToHaveTextAsync("Private");
        await Expect(deck.ActivityRows.First).ToContainTextAsync("You Created from the talk template with the Cobalt palette");
        await Expect(deck.Present).ToHaveAttributeAsync("href", new Regex($"^/d/{id}/roadmap-[0-9a-f]{{32}}$"));
    }

    [Test]
    public async Task DeckPage_WhenNoSuchDeck_ShouldSayNotFound()
    {
        var deck = new DeckPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);

        await deck.GotoAsync("NoSuchDeck00");

        await Expect(deck.NotFound).ToHaveTextAsync("Deck not found");
    }

    [Test]
    public async Task PlayerPage_WhenSlugMissing_ShouldMoveToCanonicalSlugKeepingHash()
    {
        var id = await CreateDeckAsync(TestUser.Sam, $"Kickoff {Guid.NewGuid():N}");
        // Three slides, so #3 is a real position: a redirect that dropped the hash would leave the player on #1.
        await AgentSeed.AddQuotesAsync(TestUser.Sam, id, "One change a day.", "Small batches win.", "Ship half by Friday.");
        var player = new PlayerPage(Page);

        await player.GotoAsync(id, hash: "#3");

        await Expect(Page).ToHaveURLAsync(new Regex($"/d/{id}/kickoff-[0-9a-f]{{32}}#3$"));
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Slide(3)).ToContainTextAsync("Ship half by Friday.");
    }

    [Test]
    public async Task PlayerPage_WhenPrivateDeckAndSignedOut_ShouldOfferSignIn()
    {
        var id = await CreateDeckAsync(TestUser.Sam, $"Private plan {Guid.NewGuid():N}");
        var player = new PlayerPage(Page);
        await Page.Context.ClearCookiesAsync();

        await player.GotoAsync(id);

        await Expect(player.NotFound).ToHaveTextAsync("Deck not found");
        await Expect(player.SignIn).ToBeVisibleAsync();
    }

    // Signs in as presenter, makes a deck with "+ New deck" and returns its id once its deck page is open.
    private async Task<string> CreateDeckAsync(TestUser presenter, string title)
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, presenter);
        await decks.GotoAsync();
        await decks.CreateDeckAsync(title);
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        return DeckPageUrl().Match(Page.Url).Groups["id"].Value;
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
