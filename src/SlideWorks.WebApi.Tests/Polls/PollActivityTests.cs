using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Tests.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The guest's poll activity (V3, V5): what audienceState.activity carries, read as the join page reads it (GET
// /api/join/{code}, composed by the audience feed), and the same activity asked for by code. Rounds and settings come from
// PollRowSeed; the presenter's slide is set in SettablePresentations without telling the observers, so no arrival runs. The
// app is shared by the fixture: each test makes its own deck.
[TestFixture]
public class PollActivityTests : PollTestFixture
{
    private static readonly PollSettings _upToTwo = new(PollPick.UpTo, 2, PollResults.AfterAnswer, 2, PollOpens.OnArrival, PollPrivacy.Private);

    [Test]
    public async Task Activity_WhenRoundIsOpen_ShouldCarryQuestionOptionsAndSettingsGuestsNeed()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, _upToTwo);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        var activity = await ActivityAsync(poll);

        PollActivityKeys.Of(activity).ShouldBe(PollActivityKeys.All, ignoreOrder: true);
        activity.GetProperty("type").GetString().ShouldBe("poll");
        activity.GetProperty("round").GetString().ShouldBe(round.Id.ToString());
        activity.GetProperty("number").GetInt32().ShouldBe(1);
        activity.GetProperty("phase").GetString().ShouldBe("open");
        activity.GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
        activity.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe(PollSeed.OPTIONS);
        activity.GetProperty("pick").GetString().ShouldBe("upTo");
        activity.GetProperty("max").GetInt32().ShouldBe(2);
        activity.GetProperty("results").GetString().ShouldBe("afterAnswer");
        activity.GetProperty("roomSeesCounts").GetBoolean().ShouldBeFalse();
        activity.GetProperty("correct").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Activity_WhenAnswerNotShown_ShouldHaveNullCorrect()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, _upToTwo);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 3);

        var activity = await ActivityAsync(poll);

        activity.GetProperty("phase").GetString().ShouldBe("closed");
        activity.GetProperty("round").GetString().ShouldBe(round.Id.ToString());
        activity.GetProperty("roomSeesCounts").GetBoolean().ShouldBeTrue();
        activity.GetProperty("correct").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Activity_WhenAnswerIsShown_ShouldCarryTheCorrectOption()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, _upToTwo);
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(round.Id, joinedAtClose: 3, answerShown: true);

        var activity = await ActivityAsync(poll);

        activity.GetProperty("phase").GetString().ShouldBe("closed");
        activity.GetProperty("correct").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task Activity_WhenSlideHasNoPoll_ShouldBeNull()
    {
        var poll = await SeedPollAsync();
        Presentations.Set(SettablePresentations.At(poll.DeckId, poll.OtherSlide));

        var activity = await ActivityAsync(poll);

        activity.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // Slides removed while live can leave the presenter's position past the end; the feed then shows the last slide, and the
    // card follows it.
    [Test]
    public async Task Activity_WhenPresenterIsPastTheEndOfTheDeck_ShouldBeTheLastSlidesPoll()
    {
        var poll = await SeedPollAsync();
        Presentations.Set(SettablePresentations.At(poll.DeckId, poll.Slide + 3));

        var activity = await ActivityAsync(poll);

        activity.GetProperty("type").GetString().ShouldBe("poll");
        activity.GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
        activity.GetProperty("phase").GetString().ShouldBe("draft");
    }

    [Test]
    public async Task Activity_WhenPollIsManualAndHasNoRound_ShouldBeDraftWithNullRound()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });

        var activity = await ActivityAsync(poll);

        PollActivityKeys.Of(activity).ShouldBe(PollActivityKeys.All, ignoreOrder: true);
        activity.GetProperty("phase").GetString().ShouldBe("draft");
        activity.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        activity.GetProperty("number").GetInt32().ShouldBe(1);
    }

    // A new round on a manual poll waits as a draft: the number is the draft's, and the round stays null until it opens.
    [Test]
    public async Task Activity_WhenOnlyADraftRowFollowsAClosedRound_ShouldBeDraftWithItsNumber()
    {
        var poll = await SeedPollAsync();
        await Rows.SettingsAsync(poll.SlideId, PollSettings.Default with { Opens = PollOpens.Manual });
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft, number: 2);

        var activity = await ActivityAsync(poll);

        activity.GetProperty("phase").GetString().ShouldBe("draft");
        activity.GetProperty("round").ValueKind.ShouldBe(JsonValueKind.Null);
        activity.GetProperty("number").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task Activity_WhenAskedByCode_ShouldMatchTheDecks()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, revealed: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var activities = scope.ServiceProvider.GetRequiredService<IAudienceActivity>();
        var byDeck = await activities.ForDeckAsync(poll.DeckId, null);
        var byCode = await activities.ForCodeAsync(poll.Code, null);

        byDeck.ShouldNotBeNull();
        JsonSerializer.Serialize(byCode, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.Serialize(byDeck, JsonSerializerOptions.Web));
    }

    [Test]
    public async Task Activity_WhenSessionHasEnded_ShouldBeNullByDeckAndByCode()
    {
        var poll = await SeedPollAsync();
        var ended = await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live");

        await using var scope = Factory.Services.CreateAsyncScope();
        var activities = scope.ServiceProvider.GetRequiredService<IAudienceActivity>();
        var byDeck = await activities.ForDeckAsync(poll.DeckId, null);
        var byCode = await activities.ForCodeAsync(poll.Code, null);

        ended.IsSuccessStatusCode.ShouldBeTrue();
        byDeck.ShouldBeNull();
        byCode.ShouldBeNull();
    }

    // audienceState.activity as the join page reads it.
    private async Task<JsonElement> ActivityAsync(SeededPoll poll)
    {
        await RefreshAudienceAsync(poll.DeckId);
        var body = await Client.GetFromJsonAsync<JsonElement>($"/api/join/{poll.Code}");
        return body.GetProperty("state").GetProperty("activity");
    }
}
