using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// SW-6's poll tables made general (SW-9 Q2): a deck's one Q&A poll beside its slide polls, one unclosed round per session
// per kind, a Q&A round's options, PollRows' reads by kind, and the Sw9PollKinds backfill of SW-6's rows. Rows come from
// PollRowSeed; the backfill test builds a database as SW-6 left it, with SQL, because a migration can only be tested
// against rows that already exist.
[TestFixture]
public class PollKindDataTests : PollTestFixture
{
    // These tests seed the session's Q&A round themselves through PollRowSeed, so Go live must not open one: IQaRounds is
    // the recording fake.
    protected override void ConfigurePollServices(IServiceCollection services) => FakeQaRounds.AddTo(services);

    [Test]
    public async Task PollRound_WhenSlideAndQaRoundsOpenInOneSession_ShouldBothBeAllowed()
    {
        var seeded = await SeedPollAsync();

        var slideRound = await Rows.RoundAsync(seeded.SlideId, seeded.SessionId);
        var qaRound = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);

        await using var scope = Factory.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<PollRows>();
        var openSlide = await rows.OpenRoundAsync(seeded.SessionId);
        var openQa = await rows.OpenQaRoundAsync(seeded.SessionId);
        openSlide.ShouldNotBeNull();
        openSlide.Id.ShouldBe(slideRound.Id);
        openSlide.Kind.ShouldBe(PollKind.Slide);
        openQa.ShouldNotBeNull();
        openQa.Id.ShouldBe(qaRound.Id);
        openQa.Kind.ShouldBe(PollKind.Qa);
        openQa.SlideId.ShouldBeNull();
    }

    [Test]
    public async Task PollRound_WhenSecondOpenRoundOfOneKind_ShouldBeRefusedByIndex()
    {
        var seeded = await SeedPollAsync();
        await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);

        var secondQa = await Should.ThrowAsync<DbUpdateException>(() => Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId));

        secondQa.InnerException!.Message.ShouldContain("UNIQUE constraint failed: PollRounds.SessionId, PollRounds.Kind");
    }

    [Test]
    public async Task Poll_WhenSecondQaPollForADeck_ShouldBeRefusedByIndex()
    {
        var seeded = await SeedPollAsync();
        var first = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Polls.Add(new Poll
        {
            Id = Guid.NewGuid(),
            Kind = PollKind.Qa,
            DeckId = seeded.DeckId,
            Pick = PollPick.Any,
            Results = PollResults.Live,
            Opens = PollOpens.Manual,
            Privacy = PollPrivacy.Private,
        });

        var second = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());

        second.InnerException!.Message.ShouldContain("UNIQUE constraint failed: Polls.DeckId");
        (await db.Polls.AsNoTracking().CountAsync(p => p.DeckId == seeded.DeckId && p.Kind == PollKind.Qa)).ShouldBe(1);
        (await db.Polls.AsNoTracking().SingleAsync(p => p.DeckId == seeded.DeckId && p.Kind == PollKind.Qa)).Id.ShouldBe(first.PollId);
    }

    [Test]
    public async Task OpenRoundAsync_WhenOnlyAQaRoundIsOpen_ShouldReturnNull()
    {
        var seeded = await SeedPollAsync();
        var qaRound = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);

        await using var scope = Factory.Services.CreateAsyncScope();
        var rows = scope.ServiceProvider.GetRequiredService<PollRows>();
        var open = await rows.OpenRoundAsync(seeded.SessionId);

        open.ShouldBeNull();
        (await rows.OpenQaRoundAsync(seeded.SessionId))!.Id.ShouldBe(qaRound.Id);
    }

    [Test]
    public async Task PollOption_WhenTextIs201Characters_ShouldBeRefused()
    {
        var seeded = await SeedPollAsync();
        var round = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);
        var longest = await Rows.OptionAsync(round.Id, new string('a', PollOption.TEXT_MAX));

        var tooLong = await Should.ThrowAsync<DbUpdateException>(() => Rows.OptionAsync(round.Id, new string('b', PollOption.TEXT_MAX + 1)));

        longest.Position.ShouldBe(0);
        tooLong.InnerException!.Message.ShouldContain("CHECK constraint failed: CK_PollOptions_Text_Length");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PollOptions.AsNoTracking().CountAsync(o => o.RoundId == round.Id)).ShouldBe(1);
    }

    [Test]
    public async Task QaRoundAsync_WhenSeeded_ShouldMakeOneQaPollPerDeck()
    {
        var seeded = await SeedPollAsync();
        var first = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);
        await Rows.CloseAsync(first.Id, joinedAtClose: 0);

        var second = await Rows.QaRoundAsync(seeded.DeckId, seeded.SessionId);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var qaPolls = await db.Polls.AsNoTracking().Where(p => p.DeckId == seeded.DeckId && p.Kind == PollKind.Qa).ToListAsync();
        var found = await scope.ServiceProvider.GetRequiredService<PollRows>().QaPollAsync(seeded.DeckId);
        qaPolls.Count.ShouldBe(1);
        qaPolls[0].SlideId.ShouldBeNull();
        qaPolls[0].Pick.ShouldBe(PollPick.Any);
        found.ShouldNotBeNull();
        found.Id.ShouldBe(qaPolls[0].Id);
        second.PollId.ShouldBe(first.PollId);
        second.Number.ShouldBe(2);
        first.Number.ShouldBe(1);
    }

    [Test]
    public async Task Migration_WhenSw6RowsExist_ShouldBackfillPollIdAndKind()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sw-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options);
            var migrator = db.GetService<IMigrator>();
            var all = db.Database.GetMigrations().ToList();
            await migrator.MigrateAsync(all.Single(m => m.EndsWith("_Sw6Polls", StringComparison.Ordinal)));
            var sw6 = new Sw6Rows(Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()), Upper(Guid.NewGuid()));
            await SeedSw6RowsAsync(db, sw6);

            await migrator.MigrateAsync(all.Single(m => m.EndsWith("_Sw9PollKinds", StringComparison.Ordinal)));

            var polls = await db.Database.SqlQueryRaw<string>("""
                SELECT "Id" || '|' || "SlideId" || '|' || "Kind" || '|' || "Pick" || '|' || ifnull("Max", '-') || '|' || "Results"
                    || '|' || ifnull("Correct", '-') || '|' || "Opens" || '|' || "Privacy" AS Value FROM "Polls"
                """).ToListAsync();
            var rounds = await db.Database.SqlQueryRaw<string>("""
                SELECT "PollId" || '|' || "SlideId" || '|' || "Kind" || '|' || "SessionId" || '|' || "Number" || '|' || ifnull("OpenedAt", '-')
                    || '|' || ifnull("ClosedAt", '-') || '|' || ifnull("RevealedAt", '-') || '|' || ifnull("AnswerShownAt", '-')
                    || '|' || ifnull("JoinedAtClose", '-') || '|' || ifnull("Final", '-') AS Value FROM "PollRounds"
                """).ToListAsync();
            var answers = await db.Database.SqlQueryRaw<string>("""SELECT "RoundId" || '|' || "GuestId" || '|' || "Picks" || '|' || "Revision" AS Value FROM "PollAnswers" """).ToListAsync();
            var broken = await db.Database.SqlQueryRaw<string>("""SELECT "table" AS Value FROM pragma_foreign_key_check""").ToListAsync();
            var (a, b, live, ended) = (sw6.SlideA, sw6.SlideB, sw6.LiveSession, sw6.EndedSession);
            polls.Order(StringComparer.Ordinal).ShouldBe(new[]
            {
                $"{a}|{a}|Slide|One|-|Live|-|OnArrival|Private",
                $"{b}|{b}|Slide|UpTo|2|AfterAnswer|1|Manual|Private",
            }.Order(StringComparer.Ordinal).ToList());
            rounds.Order(StringComparer.Ordinal).ShouldBe(new[]
            {
                $"{a}|{a}|Slide|{live}|1|1|2|1|2|5|{FINAL_A}",
                $"{a}|{a}|Slide|{live}|2|-|3|-|-|-|-",
                $"{a}|{a}|Slide|{live}|3|4|-|-|-|-|-",
                $"{b}|{b}|Slide|{live}|1|1|2|-|-|0|{FINAL_B}",
                $"{b}|{b}|Slide|{ended}|1|-|-|-|-|-|-",
            }.Order(StringComparer.Ordinal).ToList());
            answers.ShouldBe(new[] { $"{sw6.OpenRound}|guest-1|[0]|1" });
            broken.ShouldBeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    private const string FINAL_A = """{"options":["One","Two"],"counts":[1,0],"answered":1}""";

    private const string FINAL_B = """{"options":["One","Two"],"counts":[0,0],"answered":0}""";

    // SW-6's rows as its migration left them: two poll slides in one deck, the second with settings other than the
    // defaults. In the live session, the first slide has a closed round (revealed, answer shown, with its Final), a
    // discarded draft (no OpenedAt, ClosedAt set) and an open round with an answer; the second has a closed round numbered
    // 1 too (the same number in the same session). In an ended session the second slide has a draft (no OpenedAt, no
    // ClosedAt), so the one-unclosed-round index holds per session.
    private static async Task SeedSw6RowsAsync(AppDbContext db, Sw6Rows sw6)
    {
        var owner = Upper(Guid.NewGuid());
        const string DECK = "deck0001";
        const string SLOTS = """{"question":"Which?","options":["One","Two"]}""";
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Email, Name, GoogleSubject, CreatedAt, Access, IsAdmin) VALUES ({0}, 'sam@example.com', 'Sam', 'sub-1', 0, 'Approved', 0)", owner);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Decks (Id, Slug, Title, PartsJson, OwnerId, Sharing, Revision, CreatedAt, UpdatedAt, Template, Palette, Draft, JoinCode) "
            + "VALUES ({0}, 'd', 'Deck', '[]', {1}, 'Private', 0, 0, 0, 'talk', 'cobalt', 0, '123456')", DECK, owner);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Slides (Id, DeckId, Position, Layout, SlotsJson, Steps, Revision, DeepDive) VALUES ({0}, {1}, 0, 'poll', {2}, 0, 1, 0), ({3}, {1}, 1, 'poll', {2}, 0, 1, 0)",
            sw6.SlideA, DECK, SLOTS, sw6.SlideB);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO AudienceSessions (Id, DeckId, StartedAt, PeakJoined, EndedAt) VALUES ({0}, {1}, 0, 0, 9), ({2}, {1}, 10, 0, NULL)",
            sw6.EndedSession, DECK, sw6.LiveSession);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Polls (SlideId, DeckId, Pick, Max, Results, Correct, Opens, Privacy) "
            + "VALUES ({0}, {1}, 'One', NULL, 'Live', NULL, 'OnArrival', 'Private'), ({2}, {1}, 'UpTo', 2, 'AfterAnswer', 1, 'Manual', 'Private')",
            sw6.SlideA, DECK, sw6.SlideB);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO PollRounds (Id, SlideId, SessionId, Number, OpenedAt, ClosedAt, RevealedAt, AnswerShownAt, JoinedAtClose, Final) VALUES "
            + "({0}, {1}, {2}, 1, 1, 2, 1, 2, 5, {3}), ({4}, {1}, {2}, 2, NULL, 3, NULL, NULL, NULL, NULL), ({5}, {1}, {2}, 3, 4, NULL, NULL, NULL, NULL, NULL), "
            + "({6}, {7}, {2}, 1, 1, 2, NULL, NULL, 0, {8}), ({9}, {7}, {10}, 1, NULL, NULL, NULL, NULL, NULL, NULL)",
            Upper(Guid.NewGuid()), sw6.SlideA, sw6.LiveSession, FINAL_A, Upper(Guid.NewGuid()), sw6.OpenRound,
            Upper(Guid.NewGuid()), sw6.SlideB, FINAL_B, Upper(Guid.NewGuid()), sw6.EndedSession);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO PollAnswers (RoundId, GuestId, Picks, Revision, UpdatedAt) VALUES ({0}, 'guest-1', '[0]', 1, 4)", sw6.OpenRound);
    }

    // The ids the SW-6 seed uses, as stored (upper-case text).
    private sealed record Sw6Rows(string SlideA, string SlideB, string LiveSession, string EndedSession, string OpenRound);

    private static string Upper(Guid id) => id.ToString().ToUpperInvariant();
}
