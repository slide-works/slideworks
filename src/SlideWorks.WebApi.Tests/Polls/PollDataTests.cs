using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// SW-6's tables (V1, V2): a poll slide's settings row, the one-unclosed-round rule, PollRows' shared reads and the test
// seed that writes rounds and answers. Slides are made through the tools, rounds and answers through PollRowSeed.
[TestFixture]
public class PollDataTests : PollTestFixture
{
    private const string POLL_SLOTS = """{"question":"Which practice helped most?","options":["Pairing","Small batches","Trunk-based"]}""";

    [Test]
    public async Task AddSlide_WhenLayoutIsPoll_ShouldCreateDefaultSettings()
    {
        var seeded = await SeedPollAsync();

        var settings = await SettingsAsync(seeded.SlideId);
        var quote = await SettingsAsync(await SlideIdAsync(seeded.DeckId, seeded.OtherSlide));

        settings.ShouldBe(PollSettings.Default);
        settings!.Pick.ShouldBe(PollPick.One);
        settings.Max.ShouldBeNull();
        settings.Results.ShouldBe(PollResults.Live);
        settings.Correct.ShouldBeNull();
        settings.Opens.ShouldBe(PollOpens.OnArrival);
        settings.Privacy.ShouldBe(PollPrivacy.Private);
        quote.ShouldBeNull();
    }

    [Test]
    public async Task UpdateSlide_WhenLayoutBecomesPoll_ShouldCreateDefaultSettings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var added = await DeckSeed.AddQuoteAsync(agent, deck, "Ship it small");
        var slideId = await SlideIdAsync(deck, 1);
        var before = await SettingsAsync(slideId);

        await SlideCalls.UpdateAsync(agent, deck, 1, added.GetProperty("revision").GetInt64(), POLL_SLOTS, layout: "poll");

        before.ShouldBeNull();
        (await SettingsAsync(slideId)).ShouldBe(PollSettings.Default);
    }

    [Test]
    public async Task UpdateSlide_WhenPollBecomesQuoteAndBack_ShouldKeepItsSettings()
    {
        var seeded = await SeedPollAsync();
        var changed = new PollSettings(PollPick.UpTo, 2, PollResults.AfterClose, 3, PollOpens.Manual, PollPrivacy.Private);
        await Rows.SettingsAsync(seeded.SlideId, changed);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);

        var asQuote = await SlideCalls.UpdateAsync(agent, seeded.DeckId, seeded.Slide, await RevisionAsync(agent, seeded), """{"quote":"No poll here","attribution":"Sam"}""", layout: "quote");
        await SlideCalls.UpdateAsync(agent, seeded.DeckId, seeded.Slide, asQuote.GetProperty("revision").GetInt64(), POLL_SLOTS, layout: "poll");

        (await SettingsAsync(seeded.SlideId)).ShouldBe(changed);
    }

    [Test]
    public async Task DeleteSlide_WhenSlideIsAPollWithRounds_ShouldKeepItsRows()
    {
        var seeded = await SeedPollAsync();
        var round = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-1", 0);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);

        await SlideCalls.OkAsync(agent, SlideEdits.DELETE_TOOL, new { deck = seeded.DeckId, slide = seeded.Slide });

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Polls.CountAsync(p => p.SlideId == seeded.SlideId)).ShouldBe(1);
        (await db.PollRounds.CountAsync(r => r.SlideId == seeded.SlideId)).ShouldBe(1);
        (await db.PollAnswers.CountAsync(a => a.RoundId == round.Id)).ShouldBe(1);
    }

    [Test]
    public async Task PollRound_WhenSecondOpenRoundInSession_ShouldBeRefusedByIndex()
    {
        var seeded = await SeedPollAsync();
        var otherSlideId = await SlideIdAsync(seeded.DeckId, seeded.OtherSlide);
        await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);

        var onOtherSlide = await Should.ThrowAsync<DbUpdateException>(() => Rows.RoundAsync(otherSlideId, seeded.SessionId));
        var draftOnSameSlide = await Should.ThrowAsync<DbUpdateException>(() => Rows.RoundAsync(seeded.SlideId, seeded.SessionId, PollPhase.Draft, number: 2));

        onOtherSlide.InnerException!.Message.ShouldContain("UNIQUE constraint failed: PollRounds.SessionId");
        draftOnSameSlide.InnerException!.Message.ShouldContain("UNIQUE constraint failed: PollRounds.SessionId");
    }

    [Test]
    public async Task PollRound_WhenEarlierRoundIsClosed_ShouldAllowTheNextAndRefuseADuplicateNumber()
    {
        var seeded = await SeedPollAsync();
        var first = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);

        var second = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId, number: 2);
        await Rows.CloseAsync(second.Id, joinedAtClose: 0);
        var duplicate = await Should.ThrowAsync<DbUpdateException>(() => Rows.RoundAsync(seeded.SlideId, seeded.SessionId, number: 2));

        second.Number.ShouldBe(2);
        duplicate.InnerException!.Message.ShouldContain("UNIQUE constraint failed: PollRounds.PollId, PollRounds.SessionId, PollRounds.Number");
    }

    [Test]
    public async Task PollRows_WhenSessionHasAnOpenRound_ShouldReturnIt()
    {
        var seeded = await SeedPollAsync();
        var closed = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        await Rows.CloseAsync(closed.Id, joinedAtClose: 3);
        var open = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId, number: 2, revealed: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<PollRows>();
        var found = await rows.OpenRoundAsync(seeded.SessionId);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(open.Id);
        found.Number.ShouldBe(2);
        found.SlideId.ShouldBe(seeded.SlideId);
        found.OpenedAt.ShouldBe(Clock.GetUtcNow());
        found.RevealedAt.ShouldBe(Clock.GetUtcNow());
        found.ClosedAt.ShouldBeNull();
        found.Final.ShouldBeNull();
    }

    [Test]
    public async Task PollRows_WhenEveryRoundIsClosed_ShouldReturnNull()
    {
        var seeded = await SeedPollAsync();
        var closed = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        await Rows.CloseAsync(closed.Id, joinedAtClose: 1);

        await using var scope = Factory.Services.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<PollRows>().OpenRoundAsync(seeded.SessionId);

        found.ShouldBeNull();
    }

    [Test]
    public async Task PollRowSeed_WhenSeedingAClosedRound_ShouldWriteFinal()
    {
        var seeded = await SeedPollAsync();
        var round = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-1", 0);
        await Rows.AnswerAsync(round.Id, "guest-2", 2);
        await Rows.AnswerAsync(round.Id, "guest-2", 2, 1);
        await Rows.AnswerAsync(round.Id, "guest-3", 1);

        await Rows.CloseAsync(round.Id, joinedAtClose: 5, answerShown: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.PollRounds.AsNoTracking().SingleAsync(r => r.Id == round.Id);
        var replaced = await db.PollAnswers.AsNoTracking().SingleAsync(a => a.RoundId == round.Id && a.GuestId == "guest-2");
        var rawFinal = await db.Database.SqlQuery<string>($"SELECT Final AS Value FROM PollRounds WHERE Id = {round.Id.ToString().ToUpperInvariant()}").SingleAsync();
        stored.ClosedAt.ShouldBe(Clock.GetUtcNow());
        stored.AnswerShownAt.ShouldBe(Clock.GetUtcNow());
        stored.JoinedAtClose.ShouldBe(5);
        stored.Final.ShouldNotBeNull();
        stored.Final.Options.ShouldBe(PollSeed.OPTIONS);
        stored.Final.Counts.ShouldBe(new[] { 1, 2, 1 });
        stored.Final.Answered.ShouldBe(3);
        replaced.Picks.ShouldBe(new[] { 1, 2 });
        replaced.Revision.ShouldBe(2);
        using var json = JsonDocument.Parse(rawFinal);
        json.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(new[] { "options", "counts", "answered" });
    }

    [Test]
    public async Task PollTables_WhenCreated_ShouldCascadeNothing()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var actions = await db.Database.SqlQueryRaw<string>("""
            SELECT on_delete AS Value FROM pragma_foreign_key_list('Polls')
            UNION ALL SELECT on_delete FROM pragma_foreign_key_list('PollRounds')
            UNION ALL SELECT on_delete FROM pragma_foreign_key_list('PollAnswers')
            UNION ALL SELECT on_delete FROM pragma_foreign_key_list('PollOptions')
            """).ToListAsync();

        actions.Count.ShouldBe(7);
        actions.ShouldAllBe(action => action == "RESTRICT");
    }

    private async Task<PollSettings?> SettingsAsync(Guid slideId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PollRows>().SettingsAsync(slideId);
    }

    private async Task<Guid> SlideIdAsync(string deckId, int slide)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Slides.AsNoTracking().Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1).Select(s => s.Id).SingleAsync();
    }

    private static async Task<long> RevisionAsync(McpTestClient agent, SeededPoll seeded)
        => (await SlideCalls.SlidesAsync(agent, seeded.DeckId))[seeded.Slide - 1].GetProperty("revision").GetInt64();
}
