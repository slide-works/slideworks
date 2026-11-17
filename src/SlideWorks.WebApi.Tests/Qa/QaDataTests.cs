using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Qa;

// SW-9's rows (Q2) and their seeds: the database refuses what the contract forbids (a second question on screen in a
// round, a name over 30 characters), QaSeed gives a round even with IQaRounds faked, QaRowSeed writes a question at its
// option's position, and QaRows reads the rows back with the defaults for a deck or slide that has none. Each test seeds
// a deck of its own, since the app is shared by the fixture.
[TestFixture]
public class QaDataTests : QaTestFixture
{
    protected override void ConfigureQaServices(IServiceCollection services) => FakeQaRounds.AddTo(services);

    [Test]
    public async Task Question_WhenSecondOnScreenInARound_ShouldBeRefusedByIndex()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(seeded.RoundId, "How do you split a big change?", onScreen: true);

        var refused = await Should.ThrowAsync<DbUpdateException>(() => QaRowSeed.QuestionAsync(seeded.RoundId, "What do you ship first?", onScreen: true));

        refused.InnerException!.Message.ShouldContain("UNIQUE constraint failed: Questions.RoundId");
    }

    [Test]
    public async Task Question_WhenSecondInARoundIsOffScreen_ShouldBeWritten()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(seeded.RoundId, "How do you split a big change?", onScreen: true);

        await QaRowSeed.QuestionAsync(seeded.RoundId, "What do you ship first?");

        (await RowsAsync(rows => rows.QuestionsAsync(seeded.RoundId))).Select(q => q.OnScreen).ShouldBe([true, false]);
    }

    [Test]
    public async Task Question_WhenNameIs31Characters_ShouldBeRefused()
    {
        var seeded = await SeedQaAsync();

        var refused = await Should.ThrowAsync<DbUpdateException>(() => QaRowSeed.QuestionAsync(seeded.RoundId, "Why?", name: new string('n', 31)));

        refused.InnerException!.Message.ShouldContain("CK_Questions_Name_Length");
    }

    [Test]
    public async Task Question_WhenNameIs30Characters_ShouldBeWritten()
    {
        var seeded = await SeedQaAsync();

        var question = await QaRowSeed.QuestionAsync(seeded.RoundId, "Why?", name: new string('n', 30));

        (await RowsAsync(rows => rows.QuestionAsync(seeded.RoundId, question.Position + 1)))!.Name.ShouldBe(new string('n', 30));
    }

    [Test]
    public async Task CreateAsync_WhenRoundsAreFaked_ShouldStillReturnARound()
    {
        var seeded = await SeedQaAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var round = await scope.ServiceProvider.GetRequiredService<PollRows>().OpenQaRoundAsync(seeded.SessionId);
        round.ShouldNotBeNull();
        round.Id.ShouldBe(seeded.RoundId);
        round.Kind.ShouldBe(PollKind.Qa);
        round.SlideId.ShouldBeNull();
        round.OpenedAt.ShouldNotBeNull();
        var poll = await scope.ServiceProvider.GetRequiredService<PollRows>().QaPollAsync(seeded.DeckId);
        poll.ShouldNotBeNull();
        round.PollId.ShouldBe(poll.Id);
        poll.ToSettings().ShouldBe(new PollSettings(PollPick.Any, null, PollResults.Live, null, PollOpens.Manual, PollPrivacy.Private));
        seeded.Code.ShouldNotBeNullOrEmpty();
        (seeded.QuoteSlide, seeded.QaSlide).ShouldBe((1, 2));
        (await Presentations.GetAsync(seeded.DeckId))!.Slide.ShouldBe(seeded.QuoteSlide);
    }

    [Test]
    public async Task QuestionAsync_WhenSeeded_ShouldWriteTheOptionAndTheQuestionAtOnePosition()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(seeded.RoundId, "How do you split a big change?");

        var second = await QaRowSeed.QuestionAsync(
            seeded.RoundId, "What do you ship first?", QuestionState.Answered, hidden: true, guestId: "guest-b", name: "Ana");

        second.Position.ShouldBe(1);
        var read = await RowsAsync(rows => rows.QuestionAsync(seeded.RoundId, 2));
        read.ShouldNotBeNull();
        read.Position.ShouldBe(1);
        read.GuestId.ShouldBe("guest-b");
        read.Name.ShouldBe("Ana");
        read.State.ShouldBe(QuestionState.Answered);
        read.HiddenAt.ShouldBe(Clock.GetUtcNow());
        read.AnsweredAt.ShouldBe(Clock.GetUtcNow());
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var option = await db.PollOptions.AsNoTracking().SingleAsync(o => o.RoundId == seeded.RoundId && o.Position == 1);
        option.Text.ShouldBe("What do you ship first?");
    }

    [Test]
    public async Task VoteAsync_WhenSeeded_ShouldStoreTheNumbersAsPositions()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.QuestionAsync(seeded.RoundId, "How do you split a big change?");
        await QaRowSeed.QuestionAsync(seeded.RoundId, "What do you ship first?");

        var answer = await QaRowSeed.VoteAsync(seeded.RoundId, "guest-c", 2, 1);

        answer.Picks.ShouldBe([0, 1]);
        answer.Revision.ShouldBe(1);
    }

    [Test]
    public async Task SettingsAsync_WhenDeckHasNoRow_ShouldGiveTheDefaults()
    {
        var seeded = await SeedQaAsync();

        var settings = await RowsAsync(rows => rows.SettingsAsync(seeded.DeckId));

        settings.ShouldBe(QaSettings.Default);
    }

    [Test]
    public async Task SettingsAsync_WhenSeededTwice_ShouldGiveTheLast()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.SettingsAsync(seeded.DeckId, QaSettings.Default with { Review = true });
        var last = new QaSettings(QaQuestions.Closed, false, false, false, QaShowOn.QaSlide);

        await QaRowSeed.SettingsAsync(seeded.DeckId, last);

        (await RowsAsync(rows => rows.SettingsAsync(seeded.DeckId))).ShouldBe(last);
    }

    [Test]
    public async Task SwitchesAsync_WhenOneSlideHasSomethingOff_ShouldListOnlyItByNumber()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.SwitchAsync(seeded.QaSlideId, new QaSlideSwitch(true, false));
        await QaRowSeed.SwitchAsync(seeded.QuoteSlideId, new QaSlideSwitch(false, true));

        await QaRowSeed.SwitchAsync(seeded.QuoteSlideId, QaSlideSwitch.On);

        var switches = await RowsAsync(rows => rows.SwitchesAsync(seeded.DeckId));
        switches.Keys.ShouldBe([seeded.QaSlide]);
        switches[seeded.QaSlide].ShouldBe(new QaSlideSwitch(true, false));
        (await RowsAsync(rows => rows.SwitchAsync(seeded.QuoteSlideId))).ShouldBe(QaSlideSwitch.On);
    }

    [Test]
    public async Task ReactionAsync_WhenSeededTwice_ShouldAddToOneTotal()
    {
        var seeded = await SeedQaAsync();
        await QaRowSeed.ReactionAsync(seeded.SessionId, seeded.QuoteSlideId, ReactionEmoji.Heart, 3);

        var total = await QaRowSeed.ReactionAsync(seeded.SessionId, seeded.QuoteSlideId, ReactionEmoji.Heart, 2);

        total.Count.ShouldBe(5);
    }

    private async Task<T> RowsAsync<T>(Func<QaRows, Task<T>> read)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<QaRows>());
    }
}
