using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-6's main journey on the real app, the real hub and SW-4's real presentation registry: a poll from the presenter's
// arrival to the result. Sam's deck goes in over MCP (a join slide, a pick-one poll with live results that opens on
// arrival, a quote). Sam presents from the presenter window (the test's Page, desktop size) with the audience window, the
// big screen, as a second tab; each guest is its own browser context at phone size that opens the join link the server
// gave. Every step waits on what the screens show, never on a delay: the phone card keeps one answer in flight and the
// panel one call, so each tap waits for the phone to show it before the next.
public class Sw6PollJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const string QUESTION = "How big was your last change?";

    // Criteria: the poll opens by itself when the presenter reaches its slide, on the panel, the big screen and every phone;
    // a tap sends the answer, and the phone marks it as the guest's own and shows the count once its guest has answered;
    // the big screen and the panel count it. A phone whose guest hasn't answered shows no counts while the poll is open.
    [Test]
    public async Task Poll_WhenPresenterArrives_ShouldOpenOnThePhoneAndCountTheAnswer()
    {
        var talk = await PresentAsync("Poll arrival");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await Expect(first.Guest.ActivityWaiting).ToBeVisibleAsync();
        await Expect(first.Card.Root).ToHaveCountAsync(0);

        await MoveToAsync(talk, "ArrowRight", POLL);

        await Expect(talk.Panel.State).ToHaveTextAsync("Open");
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("0 answers");
        await Expect(talk.Screen.Question).ToHaveTextAsync(QUESTION);
        await Expect(first.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{POLL}");
        await Expect(first.Card.Root).ToBeVisibleAsync();
        await Expect(first.Card.Kind).ToHaveTextAsync("Poll · Pick one");
        await Expect(first.Card.Question).ToHaveTextAsync(QUESTION);
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await Expect(first.Card.Option(0)).ToBeEnabledAsync();
        await Expect(second.Card.Option(2)).ToBeEnabledAsync();

        await first.Card.Option(1).ClickAsync();

        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.Option(1)).ToBeCheckedAsync();
        await Expect(first.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(first.Card.ChangeHint).ToBeVisibleAsync();
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(first.Card.OptionPercent(1)).ToHaveTextAsync("100%");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("0");

        // The second guest hasn't answered: the room's tally has reached the phones, but its phone shows no count, bar or
        // percentage, and nothing is picked on it.
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.Answered).ToHaveCountAsync(0);
        await Expect(second.Card.OptionPercent(1)).ToHaveCountAsync(0);
        await Expect(second.Card.OptionBar(1)).ToHaveCountAsync(0);
        await Expect(second.Card.Option(1)).Not.ToBeCheckedAsync();
        await Expect(second.Card.Sent).ToHaveCountAsync(0);
    }

    // Criteria: tapping another option while the poll is open changes the guest's answer: the bar moves from the old
    // option to the new one on the big screen, the panel and the phone, and the answer count stays the same.
    [Test]
    public async Task Poll_WhenGuestTapsAnother_ShouldMoveTheBar()
    {
        var talk = await PresentAsync("Poll change");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");

        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await second.Card.Option(1).ClickAsync();
        await Expect(second.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("50%");

        await first.Card.Option(2).ClickAsync();

        await Expect(first.Card.OptionYou(2)).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(first.Card.Option(2)).ToBeCheckedAsync();
        await Expect(first.Card.Option(0)).Not.ToBeCheckedAsync();
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("50%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("2 answers");
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("0%");
        await Expect(first.Card.OptionPercent(2)).ToHaveTextAsync("50%");
        await Expect(first.Card.Answered).ToHaveTextAsync("2 answers so far");
        await Expect(second.Card.OptionPercent(2)).ToHaveTextAsync("50%");
        await Expect(second.Card.OptionYou(1)).ToBeVisibleAsync();
    }

    // Criteria: moving on closes the poll (D2), and the phone shows the next slide with nothing to answer; coming back
    // shows it closed, not reopened (D12). The closed poll shows its result on every phone: the guest who answered sees
    // the percentages with its own pick marked, the guest who joined before and didn't answer sees them too and is told
    // so, and a guest who joins after the close is told it joined late. Nothing can be tapped any more.
    [Test]
    public async Task Poll_WhenPresenterMovesOn_ShouldCloseAndShowTheResultOnThePhone()
    {
        var talk = await PresentAsync("Poll close");
        await using var firstPhone = await OpenPhoneAsync();
        var first = await JoinAsync(firstPhone, talk.JoinUrl);
        await using var secondPhone = await OpenPhoneAsync();
        var second = await JoinAsync(secondPhone, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");
        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(second.Card.TapHint).ToBeVisibleAsync();

        await MoveToAsync(talk, "ArrowRight", SLIDES);

        await Expect(first.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{SLIDES}");
        await Expect(first.Guest.ActivityWaiting).ToBeVisibleAsync();
        await Expect(first.Card.Root).ToHaveCountAsync(0);
        await Expect(second.Guest.ActivityWaiting).ToBeVisibleAsync();
        await Expect(talk.Panel.Empty).ToHaveCountAsync(1);
        await Expect(talk.Panel.State).ToHaveCountAsync(0);

        await MoveToAsync(talk, "ArrowLeft", POLL);

        await Expect(talk.Panel.State).ToHaveTextAsync("Closed");
        await Expect(talk.Screen.State).ToHaveTextAsync("Closed");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(first.Card.Closed).ToBeVisibleAsync();
        await Expect(first.Card.Closed).ToContainTextAsync("Poll closed");
        await Expect(first.Card.Closed).ToContainTextAsync("1 answer");
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await Expect(first.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(first.Card.OptionPercent(1)).ToHaveTextAsync("0%");
        await Expect(first.Card.Option(1)).ToBeDisabledAsync();
        await Expect(first.Card.Sent).ToHaveCountAsync(0);
        await Expect(first.Card.ChangeHint).ToHaveCountAsync(0);
        await Expect(second.Card.Closed).ToBeVisibleAsync();
        await Expect(second.Card.NoAnswer).ToBeVisibleAsync();
        await Expect(second.Card.JoinedLate).ToHaveCountAsync(0);
        await Expect(second.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(second.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(second.Card.Option(0)).ToBeDisabledAsync();

        await using var latePhone = await OpenPhoneAsync();
        var late = await JoinAsync(latePhone, talk.JoinUrl);
        await Expect(late.Guest.Slide).ToHaveAttributeAsync("data-slide", $"{POLL}");
        await Expect(late.Card.Closed).ToBeVisibleAsync();
        await Expect(late.Card.JoinedLate).ToBeVisibleAsync();
        await Expect(late.Card.NoAnswer).ToHaveCountAsync(0);
        await Expect(late.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(late.Card.Option(2)).ToBeDisabledAsync();
    }

    // Sam's deck over MCP: a join slide, the poll (pick one, live results, opens on arrival: the defaults) and a quote.
    // Sam opens the presenter window, waits for it to join the presentation, goes live from its chip and opens the
    // audience window beside it. The presenter is on slide 1 when this returns.
    private async Task<Talk> PresentAsync(string title)
    {
        string deckId;
        await using (var agent = await AgentSeed.ConnectAsync(TestUser.Sam))
        {
            var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}" });
            deckId = created.GetProperty("deck").GetString().ShouldNotBeNull();
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "join", ["slots"] = new { } });
            await AgentSeed.CallAsync(agent, "add_slide", new()
            {
                ["deck"] = deckId,
                ["layout"] = "poll",
                ["slots"] = new { question = QUESTION, options = new[] { "Under 50 lines", "50 to 500 lines", "Over 500 lines" } },
            });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
        }

        await SignIn.AsAsync(Page, TestUser.Sam);
        var presenter = new PresenterPage(Page);
        await presenter.GotoAsync(deckId);
        // The window can be driven once its join has been answered: the Now panel shows and Connecting has gone.
        await presenter.IsLiveAsync();
        await Expect(presenter.Connecting).ToHaveCountAsync(0);
        await Expect(presenter.Count).ToHaveTextAsync($"1 / {SLIDES}");
        await presenter.Live.GoLive.ClickAsync();
        await Expect(presenter.Live.End).ToBeVisibleAsync();
        var joinUrl = await ReadAsync($"/api/decks/{deckId}/live", "joinUrl");

        var audienceTab = await Context.NewPageAsync();
        var audience = new PlayerPage(audienceTab);
        await audienceTab.GotoAsync(await ReadAsync($"/api/decks/{deckId}/presenter", "audienceUrl"));
        await Expect(audience.AudienceMode).ToBeVisibleAsync();
        await Expect(audience.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(presenter.AudienceChips).ToHaveCountAsync(1);
        await Page.BringToFrontAsync();
        return new Talk(presenter, audience, new PollPanel(Page), new PollSlide(audience.Slide(POLL)), joinUrl);
    }

    // Presses a key in the presenter window and waits until the presenter window and the audience window both show slide
    // {to}, so the next step starts from the move the server answered.
    private async Task MoveToAsync(Talk talk, string key, int to)
    {
        await talk.Presenter.PressAsync(key);
        await Expect(talk.Presenter.Count).ToHaveTextAsync($"{to} / {SLIDES}");
        await Expect(talk.Audience.Layer(to)).ToHaveAttributeAsync("data-active", "true");
    }

    // A guest scans the code: the phone follows the presenter's slide.
    private static async Task<Phone> JoinAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return new Phone(guest, new GuestPollCard(page));
    }

    private async Task<string> ReadAsync(string path, string field)
    {
        var response = await Page.APIRequest.GetAsync(path);
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty(field).GetString().ShouldNotBeNull();
    }

    private Task<IBrowserContext> OpenPhoneAsync() => Browser.NewContextAsync(new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
    });

    // The presenter window, the audience window, the panel in the one and the poll slide in the other, and the join link.
    private sealed record Talk(PresenterPage Presenter, PlayerPage Audience, PollPanel Panel, PollSlide Screen, string JoinUrl);

    private sealed record Phone(GuestPage Guest, GuestPollCard Card);
}
