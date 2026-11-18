using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The live session's Q&A round (Q2, Q6) through the real QaRounds: Go live opens it unless the deck has questions off, the
// deck's one Q&A poll is made the first time, End closes the round with its Final, and EnsureAsync opens one for a live
// session that has none while questions are not off. The real settings call EnsureAsync, so the pair is tested here
// through the settings route too. Each test seeds a deck of its own, since the app is shared by the fixture.
[TestFixture]
public class QaRoundsTests : QaTestFixture
{
    [Test]
    public async Task OpenAsync_WhenQuestionsOff_ShouldOpenNoRound()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });

        var sessionId = await GoLiveAsync(deck);

        (await QaRoundsOfAsync(sessionId)).ShouldBeEmpty();
        (await QaPollsOfAsync(deck)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenAsync_WhenDeckHasNoQaPoll_ShouldCreateItOnce()
    {
        var deck = await CreateDeckAsync();

        var first = await GoLiveAsync(deck);
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        var second = await GoLiveAsync(deck);

        var poll = (await QaPollsOfAsync(deck)).ShouldHaveSingleItem();
        poll.Kind.ShouldBe(PollKind.Qa);
        poll.SlideId.ShouldBeNull();
        poll.Pick.ShouldBe(PollPick.Any);
        var firstRound = (await QaRoundsOfAsync(first)).ShouldHaveSingleItem();
        var secondRound = (await QaRoundsOfAsync(second)).ShouldHaveSingleItem();
        firstRound.PollId.ShouldBe(poll.Id);
        firstRound.Number.ShouldBe(1);
        firstRound.ClosedAt.ShouldNotBeNull();
        secondRound.PollId.ShouldBe(poll.Id);
        secondRound.Number.ShouldBe(1);
        secondRound.OpenedAt.ShouldBe(Clock.GetUtcNow());
        secondRound.ClosedAt.ShouldBeNull();
    }

    [Test]
    public async Task OpenAsync_WhenTheSessionHasARoundOpen_ShouldOpenNothing()
    {
        var seeded = await SeedQaAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IQaRounds>().OpenAsync(seeded.DeckId, seeded.SessionId);
        }

        (await QaRoundsOfAsync(seeded.SessionId)).ShouldHaveSingleItem().Id.ShouldBe(seeded.RoundId);
    }

    [Test]
    public async Task CloseAsync_WhenRoundOpen_ShouldWriteFinal()
    {
        var seeded = await SeedQaAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", seeded.Code);
        await QaRowSeed.QuestionAsync(seeded.RoundId, "How do you split a big change?");
        await QaRowSeed.QuestionAsync(seeded.RoundId, "What do you ship first?");
        await QaRowSeed.VoteAsync(seeded.RoundId, "guest-a", 1, 2);
        await QaRowSeed.VoteAsync(seeded.RoundId, "guest-b", 2);
        await QaRowSeed.VoteAsync(seeded.RoundId, "guest-c");

        (await Client.DeleteAsync($"/api/decks/{seeded.DeckId}/live")).EnsureSuccessStatusCode();

        var round = (await QaRoundsOfAsync(seeded.SessionId)).ShouldHaveSingleItem();
        round.ClosedAt.ShouldBe(Clock.GetUtcNow());
        round.JoinedAtClose.ShouldBe(1);
        round.Final.ShouldNotBeNull();
        round.Final.Options.ShouldBe(["How do you split a big change?", "What do you ship first?"]);
        round.Final.Counts.ShouldBe([1, 2]);
        round.Final.Answered.ShouldBe(2);
    }

    [Test]
    public async Task CloseAsync_WhenNoRoundOpen_ShouldDoNothing()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var sessionId = await GoLiveAsync(deck);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IQaRounds>().CloseAsync(deck, sessionId);
        }

        (await QaRoundsOfAsync(sessionId)).ShouldBeEmpty();
    }

    [Test]
    public async Task EnsureAsync_WhenLiveWithNoRound_ShouldOpenOne()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var sessionId = await GoLiveAsync(deck);
        // The settings seam saves the row before it calls EnsureAsync.
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Open });

        await EnsureAsync(deck);

        var round = (await QaRoundsOfAsync(sessionId)).ShouldHaveSingleItem();
        round.Number.ShouldBe(1);
        round.OpenedAt.ShouldNotBeNull();
        round.ClosedAt.ShouldBeNull();
    }

    [Test]
    public async Task EnsureAsync_WhenNotLive_ShouldOpenNothing()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var ended = await GoLiveAsync(deck);
        (await Client.DeleteAsync($"/api/decks/{deck}/live")).EnsureSuccessStatusCode();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Open });

        await EnsureAsync(deck);

        (await QaRoundsOfAsync(ended)).ShouldBeEmpty();
        (await QaPollsOfAsync(deck)).ShouldBeEmpty();
    }

    [Test]
    public async Task EnsureAsync_WhenLiveWithARoundOpen_ShouldOpenNothing()
    {
        var seeded = await SeedQaAsync();

        await EnsureAsync(seeded.DeckId);

        (await QaRoundsOfAsync(seeded.SessionId)).ShouldHaveSingleItem().Id.ShouldBe(seeded.RoundId);
    }

    [Test]
    public async Task EnsureAsync_WhenQuestionsAreStillOff_ShouldOpenNothing()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var sessionId = await GoLiveAsync(deck);

        await EnsureAsync(deck);

        (await QaRoundsOfAsync(sessionId)).ShouldBeEmpty();
    }

    // The pair through the real settings: IQaSettings calls EnsureAsync after any change made while questions were off.
    [Test]
    public async Task UpdateSettings_WhenQuestionsLeaveOffWhileLive_ShouldOpenTheRound()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var sessionId = await GoLiveAsync(deck);

        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/qa/settings", new { questions = "open" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await QaRoundsOfAsync(sessionId)).ShouldHaveSingleItem().ClosedAt.ShouldBeNull();
    }

    [Test]
    public async Task UpdateSettings_WhenAnotherSettingChangesWhileQuestionsStayOff_ShouldOpenNoRound()
    {
        var deck = await CreateDeckAsync();
        await QaRowSeed.SettingsAsync(deck, QaSettings.Default with { Questions = QaQuestions.Off });
        var sessionId = await GoLiveAsync(deck);

        var response = await Client.PutAsJsonAsync($"/api/decks/{deck}/qa/settings", new { review = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await QaRoundsOfAsync(sessionId)).ShouldBeEmpty();
    }

    private async Task EnsureAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IQaRounds>().EnsureAsync(deck);
    }

    private async Task<string> CreateDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner");
        return deck;
    }

    private async Task<Guid> GoLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        var live = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(live.GetProperty("sessionId").GetString()!);
    }

    private async Task<List<PollRound>> QaRoundsOfAsync(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PollRounds.AsNoTracking().Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa).ToListAsync();
    }

    private async Task<List<Poll>> QaPollsOfAsync(string deck)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Polls.AsNoTracking().Where(p => p.DeckId == deck && p.Kind == PollKind.Qa).ToListAsync();
    }
}
