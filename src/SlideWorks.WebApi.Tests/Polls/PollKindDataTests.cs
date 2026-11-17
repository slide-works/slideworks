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
            var slideA = Upper(Guid.NewGuid());
            var slideB = Upper(Guid.NewGuid());
            var session = Upper(Guid.NewGuid());
            var openRound = Upper(Guid.NewGuid());
            await SeedSw6RowsAsync(db, slideA, slideB, session, openRound);

            await migrator.MigrateAsync(all.Single(m => m.EndsWith("_Sw9PollKinds", StringComparison.Ordinal)));

            var polls = await db.Database.SqlQueryRaw<string>("""SELECT "Id" || '|' || "SlideId" || '|' || "Kind" AS Value FROM "Polls" ORDER BY "DeckId", "SlideId" """).ToListAsync();
            var rounds = await db.Database.SqlQueryRaw<string>("""SELECT "PollId" || '|' || "SlideId" || '|' || "Kind" || '|' || "Number" AS Value FROM "PollRounds" ORDER BY "SlideId", "Number" """).ToListAsync();
            var answers = await db.Database.SqlQueryRaw<string>("""SELECT "RoundId" AS Value FROM "PollAnswers" """).ToListAsync();
            var broken = await db.Database.SqlQueryRaw<string>("""SELECT "table" AS Value FROM pragma_foreign_key_check""").ToListAsync();
            var expectedPolls = new[] { $"{slideA}|{slideA}|Slide", $"{slideB}|{slideB}|Slide" }.Order(StringComparer.Ordinal).ToList();
            polls.Order(StringComparer.Ordinal).ShouldBe(expectedPolls);
            rounds.Order(StringComparer.Ordinal).ShouldBe(new[]
            {
                $"{slideA}|{slideA}|Slide|1", $"{slideA}|{slideA}|Slide|2", $"{slideB}|{slideB}|Slide|1",
            }.Order(StringComparer.Ordinal).ToList());
            answers.ShouldBe(new[] { openRound });
            broken.ShouldBeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    // SW-6's rows as its migration left them: two poll slides in one deck, a live session, a closed and an open round on
    // the first slide and a closed round numbered 1 on the second (the same number in the same session), and an answer.
    private static async Task SeedSw6RowsAsync(AppDbContext db, string slideA, string slideB, string session, string openRound)
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
            slideA, DECK, SLOTS, slideB);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO AudienceSessions (Id, DeckId, StartedAt, PeakJoined) VALUES ({0}, {1}, 0, 0)", session, DECK);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Polls (SlideId, DeckId, Pick, Results, Opens, Privacy) VALUES ({0}, {1}, 'One', 'Live', 'OnArrival', 'Private'), ({2}, {1}, 'One', 'Live', 'Manual', 'Private')",
            slideA, DECK, slideB);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO PollRounds (Id, SlideId, SessionId, Number, OpenedAt, ClosedAt) VALUES ({0}, {1}, {2}, 1, 1, 2), ({3}, {1}, {2}, 2, 3, NULL), ({4}, {5}, {2}, 1, 1, 2)",
            Upper(Guid.NewGuid()), slideA, session, openRound, Upper(Guid.NewGuid()), slideB);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO PollAnswers (RoundId, GuestId, Picks, Revision, UpdatedAt) VALUES ({0}, 'guest-1', '[0]', 1, 3)", openRound);
    }

    private static string Upper(Guid id) => id.ToString().ToUpperInvariant();
}
