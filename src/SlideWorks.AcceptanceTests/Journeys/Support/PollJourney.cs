using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// The steps the poll, word cloud, open text and Q&A journeys share, one copy: a phone's browser context, a JSON field read
// as the signed-in page, and Sam opening the presenter window. What differs between the journeys stays in each
// class: the deck it seeds, the watches on its pages and the record that holds its windows.
public static class PollJourney
{
    // A phone's browser context: a 390 by 844 viewport.
    public static Task<IBrowserContext> OpenPhoneContextAsync(IBrowser browser) => browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    // GETs the path as the page's signed-in session and returns the status, the body and one string field of the JSON.
    public static async Task<ApiRead> ReadAsync(IPage page, string path, string field)
    {
        var response = await page.APIRequest.GetAsync(path);
        var body = await response.TextAsync();
        string? value = null;
        if (response.Status == 200)
        {
            value = JsonDocument.Parse(body).RootElement.GetProperty(field).GetString();
        }

        return new ApiRead(response.Status, body, value);
    }

    // Signs Sam in on the page, opens the deck's presenter window and waits until it is a live window of the deck. The test
    // then checks the Connecting and slide-count state, goes live and reads the join link.
    public static async Task<PresenterPage> OpenPresenterAsync(IPage page, string deckId)
    {
        await SignIn.AsAsync(page, TestUser.Sam);
        var presenter = new PresenterPage(page);
        await presenter.GotoAsync(deckId);
        await presenter.IsLiveAsync();
        return presenter;
    }
}
