using System.Text.Json;
using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-6's harder journeys on the real app, the real hub and SW-4's real presentation registry: pick several, an answer
// caught by a new round, a phone whose connection drops while it answers, and an agent reading the results over MCP.
// The setup is E1a's (Sw6PollJourneyTests): Sam's deck goes in over MCP (a join slide, the poll, a quote) and its settings
// with update_poll before Sam arrives; Sam presents from the presenter window (the test's Page, desktop size) with the
// audience window, the big screen, as a second tab; each guest is its own browser context at phone size. No key is
// pressed before the presenter window has joined, each move waits until both windows show it, and each tap waits until
// the phone shows its result, since the phone keeps one answer in flight. A dropped connection is SW-5's DroppableHub: it
// closes the phone's hub socket and holds its reconnect until released.
public partial class Sw6PollTroubleJourneyTests : AcceptanceTest
{
    private const int SLIDES = 3;
    private const int POLL = 2;
    private const string QUESTION = "Which checks run before you merge?";
    private static readonly string[] OPTIONS = ["Unit tests", "Lint", "Acceptance tests"];

    // Criteria (D3, V6): with "pick up to 2" a tap only ticks a box; at the limit the other boxes lock and the foot counts
    // the picks; nothing leaves the phone until Send, and Send sends both picks as one answer, which the big screen, the
    // panel and the phone count as one answer with both options at 100%. Once sent the boxes lock; "Change my answer"
    // unlocks them with the count and Send at once. Sending the same picks again sends nothing and locks the boxes; sending
    // different picks moves the bars without adding an answer.
    [Test]
    public async Task Poll_WhenPickUpToTwo_ShouldSendBothOnSend()
    {
        var talk = await PresentAsync("Poll pick several", new() { ["pick"] = "upTo", ["max"] = 2 });
        await using var phoneContext = await OpenPhoneAsync();
        var phone = await JoinAsync(phoneContext, talk.JoinUrl);
        var answers = AnswerRequests.Count(phone.Page);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");
        await Expect(talk.Screen.Kind).ToHaveTextAsync("Poll · Pick up to 2");
        await Expect(phone.Card.Kind).ToHaveTextAsync("Poll · Pick up to 2");
        await Expect(phone.Card.OptionsMany).ToBeVisibleAsync();
        await Expect(phone.Card.PickCount).ToContainTextAsync("0 of 2 picked");
        await Expect(phone.Card.Send).ToBeDisabledAsync();

        await phone.Card.Option(0).ClickAsync();
        await Expect(phone.Card.PickCount).ToContainTextAsync("1 of 2 picked");
        await phone.Card.Option(2).ClickAsync();

        await Expect(phone.Card.PickCount).ToContainTextAsync("2 of 2 picked");
        await Expect(phone.Card.Option(0)).ToBeCheckedAsync();
        await Expect(phone.Card.Option(2)).ToBeCheckedAsync();
        await Expect(phone.Card.Option(1)).ToBeDisabledAsync();
        await Expect(phone.Card.Send).ToBeEnabledAsync();
        await Expect(talk.Panel.Answered).ToHaveTextAsync("0 answers");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("0 answers");
        answers.Sent.ShouldBe(0, "ticking boxes should send nothing before Send");

        await phone.Card.Send.ClickAsync();

        await Expect(phone.Card.Sent).ToBeVisibleAsync();
        await Expect(phone.Card.OptionYou(0)).ToBeVisibleAsync();
        await Expect(phone.Card.OptionYou(2)).ToBeVisibleAsync();
        await Expect(phone.Card.OptionYou(1)).ToHaveCountAsync(0);
        await Expect(phone.Card.Option(0)).ToBeDisabledAsync();
        await Expect(phone.Card.Option(1)).ToBeDisabledAsync();
        await Expect(phone.Card.Change).ToBeVisibleAsync();
        await Expect(phone.Card.PickCount).ToHaveCountAsync(0);
        await Expect(phone.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(phone.Card.OptionPercent(0)).ToHaveTextAsync("100%");
        await Expect(phone.Card.OptionPercent(1)).ToHaveTextAsync("0%");
        await Expect(phone.Card.OptionPercent(2)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.ManyNote).ToBeVisibleAsync();
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("1");
        answers.Sent.ShouldBe(1, "Send should send both picks as one answer");

        // "Change my answer" unlocks the boxes and brings back the count and Send at once; the same picks sent again are no
        // change, so nothing goes and the boxes lock again.
        await phone.Card.Change.ClickAsync();
        await Expect(phone.Card.Option(0)).ToBeEnabledAsync();
        await Expect(phone.Card.Option(0)).ToBeCheckedAsync();
        await Expect(phone.Card.PickCount).ToContainTextAsync("2 of 2 picked");
        await Expect(phone.Card.Send).ToBeEnabledAsync();
        await phone.Card.Send.ClickAsync();
        await Expect(phone.Card.Sent).ToBeVisibleAsync();
        await Expect(phone.Card.Change).ToBeVisibleAsync();
        await Expect(phone.Card.Option(0)).ToBeDisabledAsync();

        await phone.Card.Change.ClickAsync();
        await phone.Card.Option(0).ClickAsync();
        await Expect(phone.Card.PickCount).ToContainTextAsync("1 of 2 picked");
        await phone.Card.Option(1).ClickAsync();
        await Expect(phone.Card.PickCount).ToContainTextAsync("2 of 2 picked");
        await phone.Card.Send.ClickAsync();

        await Expect(phone.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(phone.Card.OptionYou(2)).ToBeVisibleAsync();
        await Expect(phone.Card.OptionYou(0)).ToHaveCountAsync(0);
        await Expect(phone.Card.Sent).ToBeVisibleAsync();
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("100%");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(phone.Card.Answered).ToHaveTextAsync("1 answer so far");
        answers.Sent.ShouldBe(2, "the unchanged Send should have sent nothing, and the changed one one answer");
    }

    // Criteria (V4, V6): the presenter closes the poll and starts round 2 while guests are mid-answer. A guest who had
    // ticked a box but not sent sees round 2 with nothing ticked. A guest whose answer was on its way when round 1 closed
    // (its phone's connection dropped before it heard of round 2) gets the answer refused: the phone keeps the picks and
    // says "<presenter> changed the poll. Send this pick again?". Once the connection is back the phone shows round 2 with
    // nothing ticked, and the answer it then sends counts in round 2. The refused answer counts in neither round.
    [Test]
    public async Task Poll_WhenRoundRestartsMidAnswer_ShouldAskThePhoneAgain()
    {
        var talk = await PresentAsync("Poll new round", new() { ["pick"] = "upTo", ["max"] = 2 });
        await using var tickingContext = await OpenPhoneAsync();
        var ticking = await JoinAsync(tickingContext, talk.JoinUrl);
        await using var sendingContext = await OpenPhoneAsync();
        var (sending, hub) = await JoinDroppableAsync(sendingContext, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Panel.State).ToHaveTextAsync("Open");
        await Expect(ticking.Card.PickCount).ToContainTextAsync("0 of 2 picked");
        await Expect(sending.Card.PickCount).ToContainTextAsync("0 of 2 picked");
        await ticking.Card.Option(1).ClickAsync();
        await Expect(ticking.Card.PickCount).ToContainTextAsync("1 of 2 picked");
        await sending.Card.Option(0).ClickAsync();
        await sending.Card.Option(2).ClickAsync();
        await Expect(sending.Card.PickCount).ToContainTextAsync("2 of 2 picked");
        var held = await HeldAnswers.RouteAsync(sending.Page);
        await sending.Card.Send.ClickAsync();
        await Expect(sending.Card.Sending).ToBeVisibleAsync();
        await held.Arrived;
        (await hub.DropAsync()).ShouldBe(1, "the sending phone should have one hub connection to drop");
        await Expect(sending.Guest.Reconnecting).ToBeVisibleAsync();

        await talk.Panel.Close.ClickAsync();
        await Expect(talk.Panel.State).ToHaveTextAsync("Closed");
        await talk.Panel.NewRound.ClickAsync();

        await Expect(talk.Panel.State).ToHaveTextAsync("Open");
        await Expect(talk.Panel.Round).ToHaveTextAsync("round 2");
        await Expect(talk.Panel.HistoryRound(1)).ToContainTextAsync("Round 1: 0 answers");
        await Expect(talk.Screen.Round).ToHaveTextAsync("Round 2");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("0 answers");
        await Expect(ticking.Card.Round).ToHaveTextAsync("Round 2");
        await Expect(ticking.Card.PickCount).ToContainTextAsync("0 of 2 picked");
        await Expect(ticking.Card.Option(1)).Not.ToBeCheckedAsync();
        await Expect(ticking.Card.Option(1)).ToBeEnabledAsync();
        await Expect(ticking.Card.Send).ToBeDisabledAsync();

        held.Release();

        await Expect(sending.Card.Conflict).ToBeVisibleAsync();
        await Expect(sending.Card.ConflictMessage).ToHaveTextAsync($"{TestUser.Sam.Name} changed the poll. Send this pick again?");
        await Expect(sending.Card.ConflictMessage).ToBeFocusedAsync();
        await Expect(sending.Card.Option(0)).ToBeCheckedAsync();
        await Expect(sending.Card.Option(2)).ToBeCheckedAsync();
        await Expect(sending.Card.Sent).ToHaveCountAsync(0);
        await Expect(talk.Panel.Answered).ToHaveTextAsync("0 answers");

        hub.Release();

        await Expect(sending.Guest.Reconnecting).ToBeHiddenAsync();
        await Expect(sending.Card.Round).ToHaveTextAsync("Round 2");
        await Expect(sending.Card.Conflict).ToHaveCountAsync(0);
        await Expect(sending.Card.PickCount).ToContainTextAsync("0 of 2 picked");
        await Expect(sending.Card.Option(0)).Not.ToBeCheckedAsync();
        await Expect(sending.Card.Option(2)).Not.ToBeCheckedAsync();
        await sending.Card.Option(1).ClickAsync();
        await Expect(sending.Card.PickCount).ToContainTextAsync("1 of 2 picked");
        await sending.Card.Send.ClickAsync();
        await Expect(sending.Card.Sent).ToBeVisibleAsync();
        await Expect(sending.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(talk.Panel.HistoryRound(1)).ToContainTextAsync("Round 1: 0 answers");
    }

    // Criteria (V6): a pick made while the phone's connection is down is kept on the phone as "Not sent yet" and nothing
    // is counted; when the connection comes back it sends by itself and is counted on the big screen, the panel and the
    // phone. A pick kept while the presenter closes the poll is not sent when the connection comes back: the phone says
    // the poll closed before it could be sent, and the result does not count it.
    [Test]
    public async Task Poll_WhenPhoneReconnects_ShouldSendTheQueuedPick()
    {
        var talk = await PresentAsync("Poll reconnect", new());
        await using var firstContext = await OpenPhoneAsync();
        var (first, firstHub) = await JoinDroppableAsync(firstContext, talk.JoinUrl);
        await using var secondContext = await OpenPhoneAsync();
        var (second, secondHub) = await JoinDroppableAsync(secondContext, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");
        await Expect(first.Card.TapHint).ToBeVisibleAsync();
        await Expect(second.Card.TapHint).ToBeVisibleAsync();
        (await firstHub.DropAsync()).ShouldBe(1, "the first phone should have one hub connection to drop");
        (await secondHub.DropAsync()).ShouldBe(1, "the second phone should have one hub connection to drop");
        await Expect(first.Guest.Reconnecting).ToBeVisibleAsync();
        await Expect(second.Guest.Reconnecting).ToBeVisibleAsync();

        await first.Card.Option(1).ClickAsync();
        await second.Card.Option(2).ClickAsync();

        await Expect(first.Card.Queued).ToBeVisibleAsync();
        await Expect(first.Card.QueuedMessage).ToHaveTextAsync("Not sent yet");
        await Expect(first.Card.Option(1)).ToBeCheckedAsync();
        await Expect(first.Card.Sent).ToHaveCountAsync(0);
        await Expect(second.Card.Queued).ToBeVisibleAsync();
        await Expect(second.Card.Option(2)).ToBeCheckedAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("0 answers");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("0 answers");

        firstHub.Release();

        await Expect(first.Guest.Reconnecting).ToBeHiddenAsync();
        await Expect(first.Card.Queued).ToHaveCountAsync(0);
        await Expect(first.Card.Sent).ToBeVisibleAsync();
        await Expect(first.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(first.Card.Answered).ToHaveTextAsync("1 answer so far");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("100%");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("1");
        await Expect(second.Card.Queued).ToBeVisibleAsync();

        await talk.Panel.Close.ClickAsync();
        await Expect(talk.Panel.State).ToHaveTextAsync("Closed");
        await Expect(first.Card.Closed).ToBeVisibleAsync();
        secondHub.Release();

        await Expect(second.Guest.Reconnecting).ToBeHiddenAsync();
        await Expect(second.Card.Queued).ToHaveCountAsync(0);
        await Expect(second.Card.Closed).ToBeVisibleAsync();
        await Expect(second.Card.Failed).ToHaveTextAsync("The poll closed before your answer could be sent.");
        await Expect(second.Card.NoAnswer).ToBeVisibleAsync();
        await Expect(second.Card.OptionYou(2)).ToHaveCountAsync(0);
        await Expect(second.Card.OptionPercent(1)).ToHaveTextAsync("100%");
        await Expect(second.Card.OptionPercent(2)).ToHaveTextAsync("0%");
        await Expect(talk.Screen.State).ToHaveTextAsync("Closed");
        await Expect(talk.Screen.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(talk.Panel.Answered).ToHaveTextAsync("1 answer");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("0");
    }

    // Criteria (V9 as amended): get_poll_results gives an agent the counts the big screen and the panel show, round by round,
    // never who picked what. While round 1 is open it is listed open with the slide's options and its counts so far; once
    // closed it is listed closed with the same counts. An option the agent appends while round 2 is open shows on the big
    // screen and the phones, and the results then give round 2 the four options it was answered on while round 1 keeps the
    // three it closed with; the top-level options are the slide's current four. The rate is answers over guests joined.
    [Test]
    public async Task Poll_WhenAgentReadsResults_ShouldMatchTheScreen()
    {
        var talk = await PresentAsync("Poll agent read", new());
        await using var firstContext = await OpenPhoneAsync();
        var first = await JoinAsync(firstContext, talk.JoinUrl);
        await using var secondContext = await OpenPhoneAsync();
        var second = await JoinAsync(secondContext, talk.JoinUrl);
        await using var thirdContext = await OpenPhoneAsync();
        var third = await JoinAsync(thirdContext, talk.JoinUrl);
        await MoveToAsync(talk, "ArrowRight", POLL);
        await Expect(talk.Screen.State).ToHaveTextAsync("Open");
        await first.Card.Option(0).ClickAsync();
        await Expect(first.Card.OptionYou(0)).ToBeVisibleAsync();
        await second.Card.Option(1).ClickAsync();
        await Expect(second.Card.OptionYou(1)).ToBeVisibleAsync();
        await third.Card.Option(1).ClickAsync();
        await Expect(third.Card.OptionYou(1)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("3 answers");
        await Expect(talk.Screen.Percent(0)).ToHaveTextAsync("33%");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("67%");
        await Expect(talk.Screen.Percent(2)).ToHaveTextAsync("0%");
        await Expect(talk.Panel.Count(0)).ToHaveTextAsync("1");
        await Expect(talk.Panel.Count(1)).ToHaveTextAsync("2");
        await Expect(talk.Panel.Count(2)).ToHaveTextAsync("0");
        await Expect(talk.Panel.Rate).ToHaveTextAsync("100% of 3 joined");
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);

        var open = await AgentSeed.CallAsync(agent, "get_poll_results", new() { ["deck"] = talk.DeckId, ["slide"] = POLL });

        open.GetProperty("question").GetString().ShouldBe(QUESTION);
        TextsOf(open.GetProperty("options")).ShouldBe(OPTIONS);
        var openRounds = open.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem().GetProperty("rounds");
        var openRound = openRounds.EnumerateArray().ShouldHaveSingleItem();
        openRound.GetProperty("number").GetInt32().ShouldBe(1);
        openRound.GetProperty("open").GetBoolean().ShouldBeTrue();
        TextsOf(openRound.GetProperty("options")).ShouldBe(OPTIONS);
        CountsOf(openRound).ShouldBe(new[] { 1, 2, 0 });
        openRound.GetProperty("answered").GetInt32().ShouldBe(3);
        openRound.GetProperty("joined").GetInt32().ShouldBe(3);
        openRound.GetProperty("rate").GetDouble().ShouldBe(1.0);
        open.ToString().ShouldNotContain("guest", Case.Insensitive, "the results should never say who picked what");

        await talk.Panel.Close.ClickAsync();
        await Expect(talk.Panel.State).ToHaveTextAsync("Closed");
        await Expect(talk.Screen.State).ToHaveTextAsync("Closed");
        await Expect(talk.Screen.Percent(1)).ToHaveTextAsync("67%");
        var closed = await AgentSeed.CallAsync(agent, "get_poll_results", new() { ["deck"] = talk.DeckId, ["slide"] = POLL });
        var closedRound = closed.GetProperty("sessions")[0].GetProperty("rounds").EnumerateArray().ShouldHaveSingleItem();
        closedRound.GetProperty("open").GetBoolean().ShouldBeFalse();
        CountsOf(closedRound).ShouldBe(new[] { 1, 2, 0 });
        closedRound.GetProperty("answered").GetInt32().ShouldBe(3);
        closedRound.GetProperty("joined").GetInt32().ShouldBe(3);
        closedRound.GetProperty("rate").GetDouble().ShouldBe(1.0);

        await talk.Panel.NewRound.ClickAsync();
        await Expect(talk.Panel.Round).ToHaveTextAsync("round 2");
        await Expect(talk.Screen.Round).ToHaveTextAsync("Round 2");
        await Expect(first.Card.Round).ToHaveTextAsync("Round 2");
        var added = await AgentSeed.CallAsync(agent, "add_poll_option", new() { ["deck"] = talk.DeckId, ["slide"] = POLL, ["text"] = "A review" });
        added.ToString().ShouldContain("A review");
        await Expect(talk.Screen.Row(3)).ToContainTextAsync("A review");
        await Expect(first.Card.Option(3)).ToBeEnabledAsync();
        await first.Card.Option(3).ClickAsync();
        await Expect(first.Card.OptionYou(3)).ToBeVisibleAsync();
        await second.Card.Option(3).ClickAsync();
        await Expect(second.Card.OptionYou(3)).ToBeVisibleAsync();
        await Expect(talk.Screen.Answered).ToHaveTextAsync("2 answers");
        await Expect(talk.Screen.Percent(3)).ToHaveTextAsync("100%");
        await Expect(talk.Panel.Count(3)).ToHaveTextAsync("2");
        await Expect(talk.Panel.Rate).ToHaveTextAsync("67% of 3 joined");

        var later = await AgentSeed.CallAsync(agent, "get_poll_results", new() { ["deck"] = talk.DeckId, ["slide"] = POLL });

        string[] four = [.. OPTIONS, "A review"];
        TextsOf(later.GetProperty("options")).ShouldBe(four);
        var rounds = later.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem().GetProperty("rounds").EnumerateArray().ToList();
        rounds.Select(r => r.GetProperty("number").GetInt32()).ToArray().ShouldBe(new[] { 1, 2 });
        rounds[0].GetProperty("open").GetBoolean().ShouldBeFalse();
        TextsOf(rounds[0].GetProperty("options")).ShouldBe(OPTIONS);
        CountsOf(rounds[0]).ShouldBe(new[] { 1, 2, 0 });
        rounds[1].GetProperty("open").GetBoolean().ShouldBeTrue();
        TextsOf(rounds[1].GetProperty("options")).ShouldBe(four);
        CountsOf(rounds[1]).ShouldBe(new[] { 0, 0, 0, 2 });
        rounds[1].GetProperty("answered").GetInt32().ShouldBe(2);
        rounds[1].GetProperty("joined").GetInt32().ShouldBe(3);
        rounds[1].GetProperty("rate").GetDouble().ShouldBe(2.0 / 3, 0.001);
    }

    // Sam's deck over MCP: a join slide, the poll and a quote, with the poll's settings changed by update_poll before Sam
    // arrives (none changed: pick one, live results, opens on arrival). Sam opens the presenter window, waits for it to
    // join the presentation, goes live from its chip and opens the audience window beside it. The presenter is on slide 1
    // when this returns.
    private async Task<Talk> PresentAsync(string title, Dictionary<string, object?> settings)
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
                ["slots"] = new { question = QUESTION, options = OPTIONS },
            });
            await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deckId, ["layout"] = "quote", ["slots"] = new { quote = "Start small.", attribution = "Sam Rivera" } });
            if (settings.Count > 0)
            {
                await AgentSeed.CallAsync(agent, "update_poll", new(settings) { ["deck"] = deckId, ["slide"] = POLL });
            }
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
        return new Talk(deckId, presenter, audience, new PollPanel(Page), new PollSlide(audience.Slide(POLL)), joinUrl);
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
        return new Phone(page, guest, new GuestPollCard(page));
    }

    // As JoinAsync, on a phone whose hub connection the test can drop (set up before the page navigates).
    private static async Task<(Phone Phone, DroppableHub Hub)> JoinDroppableAsync(IBrowserContext context, string joinUrl)
    {
        var page = await context.NewPageAsync();
        var hub = await DroppableHub.RouteAsync(page);
        var guest = new GuestPage(page);
        await guest.GotoUrlAsync(joinUrl);
        await guest.IsFollowingAsync();
        return (new Phone(page, guest, new GuestPollCard(page)), hub);
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

    private static string[] TextsOf(JsonElement texts) => [.. texts.EnumerateArray().Select(t => t.GetString().ShouldNotBeNull())];

    private static int[] CountsOf(JsonElement round) => [.. round.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32())];

    // The presenter window, the audience window, the panel in the one and the poll slide in the other, and the join link.
    private sealed record Talk(string DeckId, PresenterPage Presenter, PlayerPage Audience, PollPanel Panel, PollSlide Screen, string JoinUrl);

    private sealed record Phone(IPage Page, GuestPage Guest, GuestPollCard Card);

    // Counts the answers a phone sends (PUT …/poll/answer), as the browser issues them.
    private sealed class AnswerRequests
    {
        private int _sent;

        public int Sent => Volatile.Read(ref _sent);

        public static AnswerRequests Count(IPage page)
        {
            var requests = new AnswerRequests();
            page.Request += (_, request) =>
            {
                if (request.Method == "PUT" && AnswerPath().IsMatch(request.Url))
                {
                    Interlocked.Increment(ref requests._sent);
                }
            };
            return requests;
        }
    }

    // Holds a phone's answers (PUT …/poll/answer) before they reach the server, until Release lets them through.
    private sealed class HeldAnswers
    {
        private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Completes when the first answer has been caught.
        public Task Arrived => _arrived.Task;

        public static async Task<HeldAnswers> RouteAsync(IPage page)
        {
            var held = new HeldAnswers();
            await page.RouteAsync(AnswerPath(), async route =>
            {
                held._arrived.TrySetResult();
                await held._released.Task;
                await route.ContinueAsync();
            });
            return held;
        }

        public void Release() => _released.TrySetResult();
    }

    [GeneratedRegex(@"/api/join/[^/]+/poll/answer$")]
    private static partial Regex AnswerPath();
}
