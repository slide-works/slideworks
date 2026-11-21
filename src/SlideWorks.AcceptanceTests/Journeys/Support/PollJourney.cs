using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// The steps the poll, word cloud, open text and Q&A journeys share, one copy: a phone's browser context, a JSON field read
// as the signed-in page, and Sam going live from the presenter window. What differs between the journeys stays in each
// class: the deck it seeds, the watches on its pages and the record that holds its windows.
public static class PollJourney
{
    // A phone's browser context: a 390 by 844 viewport.
    public static Task<IBrowserContext> OpenPhoneContextAsync(IBrowser browser) => browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    // GETs the path as the page's signed-in session and returns one string field of the JSON. Fails the test on a non-200.
    public static async Task<string> ReadAsync(IPage page, string path, string field)
    {
        var response = await page.APIRequest.GetAsync(path);
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString().ShouldNotBeNull();
    }

    // Signs Sam in on the page, opens the deck's presenter window, waits until it has joined the presentation (the Now panel
    // shows and Connecting has gone) and is on slide 1 of slides, then goes live from its chip. Returns the window and the
    // deck's join link.
    public static async Task<(PresenterPage Presenter, string JoinUrl)> GoLiveAsync(IPage page, string deckId, int slides)
    {
        await SignIn.AsAsync(page, TestUser.Sam);
        var presenter = new PresenterPage(page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        await Assertions.Expect(presenter.Connecting).ToHaveCountAsync(0);
        await Assertions.Expect(presenter.Count).ToHaveTextAsync($"1 / {slides}");
        await presenter.Live.GoLive.ClickAsync();
        await Assertions.Expect(presenter.Live.End).ToBeVisibleAsync();
        return (presenter, await ReadAsync(page, $"/api/decks/{deckId}/live", "joinUrl"));
    }
}
