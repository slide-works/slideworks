using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Email;

namespace SlideWorks.AcceptanceTests.Journeys;

// A person with no SlideWorks session opens the unsubscribe link from a share email: the page asks, one press stops the
// emails, and the fragment survives the router. The token is made by the app's own IUnsubscribeLinks.
public class UnsubscribeJourney : AcceptanceTest
{
    [Test]
    public async Task Unsubscribe_WhenLinkOpenedSignedOut_ShouldConfirm()
    {
        var address = $"u-{Guid.NewGuid():N}@recipient.example";
        string link;
        using (var scope = AcceptanceApp.Services.CreateScope())
        {
            link = scope.ServiceProvider.GetRequiredService<IUnsubscribeLinks>().For(address);
        }
        var page = Page.GetByTestId("unsubscribe-page");

        await Page.GotoAsync(link);

        await Expect(page.GetByTestId("unsubscribe-heading")).ToHaveTextAsync("Stop emails from SlideWorks?");
        await Expect(Page.GetByTestId("unsubscribe-done")).ToHaveCountAsync(0);
        await page.GetByTestId("unsubscribe-stop").ClickAsync();

        await Expect(Page.GetByTestId("unsubscribe-done")).ToContainTextAsync("SlideWorks won’t email this address again.");
        await Expect(page.GetByTestId("unsubscribe-stop")).ToHaveCountAsync(0);
        await Expect(page).Not.ToContainTextAsync(address);
        using var check = AcceptanceApp.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IEmailSuppressions>().ContainsAsync(address)).ShouldBeTrue();
    }
}
