using SlideWorks.AcceptanceTests.Pages;

namespace SlideWorks.AcceptanceTests;

// /j/{code} and /{code} open the guest page for anyone. The page's root is there whatever the API answers, so these tests
// don't need a deck or a session.
public class GuestRouteTests : AcceptanceTest
{
    [Test]
    public async Task Open_WhenSixDigitPath_ShouldMountGuestPage()
    {
        var guest = new GuestPage(Page);

        await guest.GotoBareAsync("482913");

        await guest.IsLoadedAsync();
    }
}
