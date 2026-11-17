using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;
using System.Text.Json;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests;

// The keypad at /join: a wrong code keeps the digits and says what to check, then the right code reaches the guest page.
// The deck is made and taken live through the API with the browser's own session, then the browser goes anonymous.
public class JoinCodeTests : AcceptanceTest
{
    [Test]
    public async Task Type_WhenWrongThenRightCode_ShouldReachGuestPage()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        var id = await PostAsync("/api/decks", new { title = $"Join {Guid.NewGuid():N}" }, 201, "id");
        var code = await PostAsync($"/api/decks/{id}/live", null, 200, "code");
        await Page.Context.ClearCookiesAsync();
        var wrong = code[..5] + (code[5] == '9' ? '8' : (char)(code[5] + 1));
        var keypad = new JoinCodePage(Page);
        var guest = new GuestPage(Page);

        await keypad.GotoAsync();
        await keypad.IsLoadedAsync();
        await keypad.TypeAsync(wrong);
        await Expect(guest.Root).ToBeVisibleAsync();
        await Expect(keypad.Error).ToBeFocusedAsync();
        await Expect(keypad.Box(6)).ToHaveValueAsync(wrong[5].ToString());
        await keypad.Box(6).PressAsync("Backspace");
        await keypad.Box(6).PressSequentiallyAsync(code[5].ToString());

        await Expect(guest.Joined.Or(guest.Following)).ToBeVisibleAsync();
        Page.Url.ShouldEndWith($"/j/{code}");
    }

    private async Task<string> PostAsync(string path, object? body, int status, string field)
    {
        var response = await Page.APIRequest.PostAsync(path, new APIRequestContextOptions
        {
            DataObject = body,
            Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl },
        });
        response.Status.ShouldBe(status, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString()!;
    }
}
