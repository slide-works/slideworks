using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests;

// The player for people other than the owner, by the deck's sharing level (K8). The player calls only anonymous
// endpoints, so a pending person named as a viewer (SW-54 D2) stays on it instead of being sent to /pending. Each test
// makes its own deck as Sam through "+ New deck" and sets its sharing through the sharing API, because the acceptance
// database is shared by the whole run.
public partial class PlayerSharingTests : AcceptanceTest
{
    [Test]
    public async Task PlayerPage_WhenPendingNamedViewer_ShouldStayOnThePlayer()
    {
        var pending = TestUser.NewPending();
        var id = await CreateSharedDeckAsync("named", pending.Email);
        var player = new PlayerPage(Page);
        await Page.Context.ClearCookiesAsync();
        await SignIn.AsAsync(Page, pending);

        await player.GotoAsync(id);

        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/d/{id}/shared-[0-9a-f]{{32}}(#[0-9.]*)?$"));
        await Expect(new PendingPage(Page).Root).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PlayerPage_WhenLinkAndSignedOut_ShouldShowTheDeck()
    {
        var id = await CreateSharedDeckAsync("link");
        var player = new PlayerPage(Page);
        await Page.Context.ClearCookiesAsync();

        await player.GotoAsync(id);

        await Expect(player.Player).ToBeVisibleAsync();
    }

    // Someone signed in but not on the list sees "not found", like an unknown deck, even while pending: the player's
    // only gated call is /api/me, which never sends a pending person away.
    [Test]
    public async Task PlayerPage_WhenNamedAndNotListed_ShouldSayNotFound()
    {
        var id = await CreateSharedDeckAsync("named", TestUser.Alex.Email);
        var player = new PlayerPage(Page);
        await Page.Context.ClearCookiesAsync();
        await SignIn.AsAsync(Page, TestUser.NewPending());

        await player.GotoAsync(id);

        await Expect(player.NotFound).ToHaveTextAsync("Deck not found");
        await Expect(Page).ToHaveURLAsync(new Regex($"/d/{id}$"));
    }

    // Signs in as Sam, makes a deck with "+ New deck", and shares it through PUT /api/decks/{id}/sharing with the
    // browser's own session, as the sharing dialog does. It ends on about:blank: the deck page re-reads the deck on join
    // and on the DeckChanged the PUT publishes, and a re-read landing after the test swaps the session gets 401/403 and
    // sends the page to /signin or /pending, which aborts the test's next navigation.
    private async Task<string> CreateSharedDeckAsync(string level, params string[] viewers)
    {
        var decks = new DecksPage(Page);
        await SignIn.AsAsync(Page, TestUser.Sam);
        await decks.GotoAsync();
        await decks.CreateDeckAsync($"Shared {Guid.NewGuid():N}");
        await Expect(Page).ToHaveURLAsync(DeckPageUrl());
        var id = DeckPageUrl().Match(Page.Url).Groups["id"].Value;
        var response = await Page.APIRequest.PutAsync($"/api/decks/{id}/sharing", new APIRequestContextOptions
        {
            DataObject = new { level, viewers },
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(200, await response.TextAsync());
        await Page.GotoAsync("about:blank");
        return id;
    }

    [GeneratedRegex(@"/decks/(?<id>[0-9A-Za-z]{12})$")]
    private static partial Regex DeckPageUrl();
}
