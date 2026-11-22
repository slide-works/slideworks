using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

public static class Sessions
{
    // Swaps the page's session for another user's. It leaves the current page first (a re-read the page still has out, on
    // join or on a DeckChanged, could land after the swap and send the page away, aborting the next navigation), clears the
    // context's cookies, then signs the user in.
    public static async Task SwitchSessionAsync(IPage page, TestUser user)
    {
        await page.GotoAsync("about:blank");
        await page.Context.ClearCookiesAsync();
        await SignIn.AsAsync(page, user);
    }
}
