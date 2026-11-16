using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Google;

// A person an admin rejected can't sign in again. On a host of its own because Ada acts here through the admin API, and
// GoogleSignInTests needs a host where Ada has never signed in (the SLIDEWORKS_ADMIN_EMAIL bootstrap).
[TestFixture]
public class RejectedSignInTests : GoogleSignInFixture
{
    [Test]
    public async Task SignIn_WhenRejected_ShouldRedirectNotApproved()
    {
        using var first = Browser();
        using var firstSignIn = await SignInAsync(first, "sub-rejected", "rejected@example.test");
        await RejectAsync("rejected@example.test");
        using var browser = Browser();

        using var response = await SignInAsync(browser, "sub-rejected", "rejected@example.test");

        response.Headers.Location!.OriginalString.ShouldBe("/signin?error=not_approved&returnTo=%2Fdecks");
        await ShouldHaveNoSessionAsync(browser);
        await ShouldHaveNoSessionAsync(first);
    }

    // Ada rejects the person through the admin API, as an admin would on the admin page.
    private async Task RejectAsync(string email)
    {
        using var ada = ClientFor(TestUser.Ada);
        var view = await ada.GetFromJsonAsync<JsonElement>("/api/admin/users");
        var id = view.GetProperty("users").EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == email)
            .GetProperty("id").GetGuid();

        using var rejected = await ada.PostAsync($"/api/admin/users/{id}/reject", content: null);

        rejected.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
