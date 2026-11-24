using SlideWorks.AcceptanceTests.Pages;
using static Microsoft.Playwright.Assertions;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// The steps of a live talk the journeys share, one copy: the audience window, a guest following on a phone, the Questions
// tab and a move of the presenter. What differs between the journeys (the watches on the pages, the record that holds the
// windows and the phone) stays in each class.
public static class Talks
{
    // Opens the audience window in the tab (made by the caller, so a watch can be on it before it navigates) at the audience
    // URL the owner's page reads, and waits until it shows slide 1 and the presenter counts one audience chip.
    public static async Task<PlayerPage> OpenAudienceAsync(IPage owner, IPage audienceTab, PresenterPage presenter, string deckId)
    {
        var audience = new PlayerPage(audienceTab);
        var audienceRead = await PollJourney.ReadAsync(owner, $"/api/decks/{deckId}/presenter", "audienceUrl");
        audienceRead.Status.ShouldBe(200, audienceRead.Body);
        await audienceTab.GotoAsync(audienceRead.Value.ShouldNotBeNull());
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        return audience;
    }

    // A guest scans the code in the page (made by the caller, so a watch can be on it before it navigates): the phone
    // follows the presenter's slide.
    public static async Task<GuestPage> FollowAsync(IPage page, string joinUrl)
    {
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return guest;
    }

    // As FollowAsync, then opens the Questions tab and waits for its list to load.
    public static async Task<(GuestPage Guest, GuestQuestions Questions)> OpenQuestionsAsync(IPage page, string joinUrl, float timeout)
    {
        var guest = await FollowAsync(page, joinUrl);
        var questions = new GuestQuestions(page);
        await questions.QuestionsTab.ClickAsync();
        await Expect(questions.List).ToBeVisibleAsync(new() { Timeout = timeout });
        await Expect(questions.ListLoading).ToHaveCountAsync(0);
        return (guest, questions);
    }

    // Presses a key in the presenter window and waits until the presenter window (of {slides} slides) and the audience
    // window both show slide {to}, so the next step starts from the move the server answered. A null timeout is
    // Playwright's default.
    public static async Task MoveToAsync(PresenterPage presenter, PlayerPage audience, int slides, string key, int to, float? timeout = null)
    {
        var options = timeout is { } t ? new LocatorAssertionsToHaveTextOptions { Timeout = t } : null;
        var attribute = timeout is { } a ? new LocatorAssertionsToHaveAttributeOptions { Timeout = a } : null;
        await presenter.PressAsync(key);
        await Expect(presenter.Count).ToHaveTextAsync($"{to} / {slides}", options);
        await Expect(audience.Layer(to)).ToHaveAttributeAsync("data-active", "true", attribute);
    }
}
