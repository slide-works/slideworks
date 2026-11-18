using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// The real IQaSettings (Q1, Q6): the deck's settings row, the slide switch rows, the activity entry, and what it calls after
// the change. IQaRounds is FakeQaRounds (what Go live opened and what a change ensured), the relay is FakeAudienceRelay
// and the publisher FakeQuestionPublisher, each wrapped to note the order of the refresh and the publish per deck. The
// room a change leaves is read from the real IAudienceRoom. Each test seeds a live deck of its own (QaSeed).
[TestFixture]
[CancelAfter(30000)]
public class QaSettingsTests : QaTestFixture
{
    private const string REFRESH = "refresh";
    private const string PUBLISH = "publish";

    private FakeQaRounds Rounds => Factory.Services.GetRequiredService<FakeQaRounds>();

    private FakeQuestionPublisher Publisher => Factory.Services.GetRequiredService<FakeQuestionPublisher>();

    private CallOrder Order => Factory.Services.GetRequiredService<CallOrder>();

    protected override void ConfigureQaServices(IServiceCollection services)
    {
        FakeQaRounds.AddTo(services);
        FakeAudienceRelay.AddTo(services);
        FakeQuestionPublisher.AddTo(services);
        services.AddSingleton<CallOrder>();
        services.AddSingleton<IAudienceRelay>(provider => new OrderedRelay(
            provider.GetRequiredService<FakeAudienceRelay>(), provider.GetRequiredService<CallOrder>(), provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IQuestionPublisher>(provider => new OrderedPublisher(provider.GetRequiredService<FakeQuestionPublisher>(), provider.GetRequiredService<CallOrder>()));
    }

    [Test]
    public async Task UpdateAsync_WhenOneFieldGiven_ShouldLeaveTheOthers()
    {
        var qa = await SeedQaAsync();
        var seeded = new QaSettings(QaQuestions.Closed, true, false, false, QaShowOn.QaSlide);
        await QaRowSeed.SettingsAsync(qa.DeckId, seeded);

        var updated = await UpdateAsync(qa.DeckId, new QaSettingsPatch(null, false, null, null, null));

        updated.ShouldBe(seeded with { Review = false });
        (await GetAsync(qa.DeckId)).ShouldBe(seeded with { Review = false });
        (await StoredSettingsAsync(qa.DeckId)).ShouldBe(seeded with { Review = false });
    }

    [Test]
    public async Task UpdateAsync_WhenDeckHasNoRow_ShouldMakeItWithTheDefaultsAndTheChange()
    {
        var qa = await SeedQaAsync();
        (await StoredSettingsAsync(qa.DeckId)).ShouldBeNull();

        var updated = await UpdateAsync(qa.DeckId, new QaSettingsPatch(null, null, null, false, QaShowOn.QaSlide));

        updated.ShouldBe(QaSettings.Default with { Reactions = false, ShowOn = QaShowOn.QaSlide });
        (await StoredSettingsAsync(qa.DeckId)).ShouldBe(updated);
    }

    [Test]
    public async Task UpdateAsync_WhenQuestionsLeaveOffWhileLive_ShouldEnsureARound()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default with { Questions = QaQuestions.Off });

        await UpdateAsync(qa.DeckId, new QaSettingsPatch(QaQuestions.Closed, null, null, null, null));
        await UpdateAsync(qa.DeckId, new QaSettingsPatch(QaQuestions.Open, null, null, null, null));

        Rounds.CallsFor(qa.DeckId).Count(c => c == "Ensure").ShouldBe(1);
        (await GetAsync(qa.DeckId)).Questions.ShouldBe(QaQuestions.Open);
    }

    [Test]
    public async Task UpdateAsync_WhenChanged_ShouldRefreshThenPublish()
    {
        var qa = await SeedQaAsync();
        var entries = await ActivityCountAsync(qa.DeckId);

        var response = await Client.PutAsJsonAsync($"/api/decks/{qa.DeckId}/qa/settings", new { review = true, names = false });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Order.For(qa.DeckId).ShouldBe([$"{REFRESH} after {entries + 1} entries", PUBLISH]);
        (await DeckSeed.ActivityAsync(Client, qa.DeckId))[0].GetProperty("summary").GetString().ShouldBe("turned review on, turned names off");
        (await GetAsync(qa.DeckId)).ShouldBe(QaSettings.Default with { Review = true, Names = false });
    }

    [Test]
    public async Task UpdateAsync_WhenNothingChanges_ShouldWriteLogAndPublishNothing()
    {
        var qa = await SeedQaAsync();
        var entries = await ActivityCountAsync(qa.DeckId);

        var updated = await UpdateAsync(qa.DeckId, new QaSettingsPatch(QaQuestions.Open, false, true, null, null));

        updated.ShouldBe(QaSettings.Default);
        (await StoredSettingsAsync(qa.DeckId)).ShouldBeNull();
        (await ActivityCountAsync(qa.DeckId)).ShouldBe(entries);
        Order.For(qa.DeckId).ShouldBeEmpty();
        Publisher.PublishesOf(qa.DeckId).ShouldBe(0);
    }

    [Test]
    public async Task UpdateAsync_WhenQuestionsGoOffWhileLive_ShouldKeepTheRoundAndGiveTheRoomNone()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?");

        await UpdateAsync(qa.DeckId, new QaSettingsPatch(QaQuestions.Off, null, null, null, null));

        var room = await RoomAsync(qa.DeckId);
        room.ShouldBe(new QaRoom(null, QaRoomQuestions.Off, true, true, 0));
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PollRounds.AsNoTracking().SingleAsync(r => r.Id == qa.RoundId)).ClosedAt.ShouldBeNull();
        (await db.Questions.AsNoTracking().CountAsync(q => q.RoundId == qa.RoundId)).ShouldBe(1);
        Rounds.CallsFor(qa.DeckId).ShouldNotContain(c => c.StartsWith("Close", StringComparison.Ordinal));
    }

    [Test]
    public async Task UpdateAsync_WhenReviewTurnsOn_ShouldLeaveOpenQuestionsOpen()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(qa.RoundId, "How do you split a big change?");

        await UpdateAsync(qa.DeckId, new QaSettingsPatch(null, true, null, null, null));

        await using var scope = Factory.Services.CreateAsyncScope();
        var question = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Questions.AsNoTracking().SingleAsync(q => q.RoundId == qa.RoundId);
        question.State.ShouldBe(QuestionState.Open);
    }

    [Test]
    public async Task SetSlideAsync_WhenSlideUnknown_ShouldBeNotFound()
    {
        var qa = await SeedQaAsync();
        var entries = await ActivityCountAsync(qa.DeckId);

        var past = await Should.ThrowAsync<NotFoundException>(() => SetSlideAsync(qa.DeckId, 9, new QaSlidePatch(false, null)));
        var zero = await Should.ThrowAsync<NotFoundException>(() => SetSlideAsync(qa.DeckId, 0, new QaSlidePatch(false, null)));

        past.Message.ShouldBe(QaSettingsCommands.SLIDE_NOT_FOUND);
        zero.Message.ShouldBe(QaSettingsCommands.SLIDE_NOT_FOUND);
        (await ActivityCountAsync(qa.DeckId)).ShouldBe(entries);
        Order.For(qa.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task SetSlideAsync_WhenDeckHasReactionsOff_ShouldStoreTheSwitchAndTheRoomStayOff()
    {
        var qa = await SeedQaAsync();
        await QaRowSeed.SettingsAsync(qa.DeckId, QaSettings.Default with { Reactions = false });

        var off = await SetSlideAsync(qa.DeckId, qa.QuoteSlide, new QaSlidePatch(null, false));
        var roomWhenOff = await RoomAsync(qa.DeckId);
        var on = await SetSlideAsync(qa.DeckId, qa.QuoteSlide, new QaSlidePatch(null, true));
        var roomWhenOn = await RoomAsync(qa.DeckId);

        off.ShouldBe(new QaSlideSwitch(true, false));
        on.ShouldBe(QaSlideSwitch.On);
        (await StoredSwitchAsync(qa.QuoteSlideId)).ShouldBe(QaSlideSwitch.On);
        roomWhenOff!.Reactions.ShouldBeFalse();
        roomWhenOn!.Reactions.ShouldBeFalse();
        Order.For(qa.DeckId).ShouldBe([$"{REFRESH} after {await ActivityCountAsync(qa.DeckId) - 1} entries", PUBLISH, $"{REFRESH} after {await ActivityCountAsync(qa.DeckId)} entries", PUBLISH]);
    }

    [Test]
    public async Task SetSlideAsync_WhenPatchLeavesBothOn_ShouldMakeNoRow()
    {
        var qa = await SeedQaAsync();

        var slideSwitch = await SetSlideAsync(qa.DeckId, qa.QaSlide, new QaSlidePatch(true, true));

        slideSwitch.ShouldBe(QaSlideSwitch.On);
        (await StoredSwitchAsync(qa.QaSlideId)).ShouldBeNull();
        Order.For(qa.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task SetSlideAsync_WhenBothTurnedOff_ShouldLogTheSlide()
    {
        var qa = await SeedQaAsync();

        var slideSwitch = await SetSlideAsync(qa.DeckId, qa.QaSlide, new QaSlidePatch(false, false));

        slideSwitch.ShouldBe(new QaSlideSwitch(false, false));
        (await StoredSwitchAsync(qa.QaSlideId)).ShouldBe(slideSwitch);
        await using var scope = Factory.Services.CreateAsyncScope();
        var newest = (await scope.ServiceProvider.GetRequiredService<IActivityLog>().ListAsync(qa.DeckId, 1)).ShouldHaveSingleItem();
        newest.Summary.ShouldBe($"turned questions off and reactions off on slide {qa.QaSlide}");
    }

    private async Task<QaSettings> UpdateAsync(string deckId, QaSettingsPatch patch)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQaSettings>().UpdateAsync(deckId, patch);
    }

    private async Task<QaSlideSwitch> SetSlideAsync(string deckId, int slide, QaSlidePatch patch)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQaSettings>().SetSlideAsync(deckId, slide, patch);
    }

    private async Task<QaSettings> GetAsync(string deckId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQaSettings>().GetAsync(deckId);
    }

    private async Task<QaRoom?> RoomAsync(string deckId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAudienceRoom>().ForDeckAsync(deckId);
    }

    // The deck's settings row as stored, or null when it has none; no test writes through this.
    private async Task<QaSettings?> StoredSettingsAsync(string deckId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().QaSettingsRows.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId);
        return row?.ToSettings();
    }

    // The slide's switch row as stored, or null when it has none; no test writes through this.
    private async Task<QaSlideSwitch?> StoredSwitchAsync(Guid slideId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        QaSlideSwitchRow? row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().QaSlideSwitchRows.AsNoTracking().SingleOrDefaultAsync(s => s.SlideId == slideId);
        return row?.ToSwitch();
    }

    private async Task<int> ActivityCountAsync(string deckId) => await ActivityCountAsync(Factory.Services.GetRequiredService<IServiceScopeFactory>(), deckId);

    private static async Task<int> ActivityCountAsync(IServiceScopeFactory scopes, string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IActivityLog>().ListAsync(deckId, 1000)).Count;
    }

    // The relay's refresh and the publisher's call for each deck, in the order they happened. A refresh notes how many
    // activity entries the deck had by then, which shows the entry was recorded before it.
    private sealed class CallOrder
    {
        private readonly Lock _gate = new();
        private readonly List<(string DeckId, string Call)> _calls = [];

        public void Add(string deckId, string call)
        {
            lock (_gate)
            {
                _calls.Add((deckId, call));
            }
        }

        public IReadOnlyList<string> For(string deckId)
        {
            lock (_gate)
            {
                return [.. _calls.Where(c => c.DeckId == deckId).Select(c => c.Call)];
            }
        }
    }

    private sealed class OrderedRelay(FakeAudienceRelay inner, CallOrder order, IServiceScopeFactory scopes) : IAudienceRelay
    {
        public Task<AudienceState?> CurrentAsync(string deckId) => inner.CurrentAsync(deckId);

        public Task StartedAsync(string deckId) => inner.StartedAsync(deckId);

        public Task EndedAsync(string deckId) => inner.EndedAsync(deckId);

        public async Task RefreshAsync(string deckId)
        {
            order.Add(deckId, $"{REFRESH} after {await ActivityCountAsync(scopes, deckId)} entries");
            await inner.RefreshAsync(deckId);
        }
    }

    private sealed class OrderedPublisher(FakeQuestionPublisher inner, CallOrder order) : IQuestionPublisher
    {
        public Task PublishAsync(string deckId)
        {
            order.Add(deckId, PUBLISH);
            return inner.PublishAsync(deckId);
        }
    }
}
