using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Writes SW-9's rows straight to the database, for tests only (Q2): a Q&A round, questions in any state, a guest's upvotes,
// a deck's settings, a slide's switch, reaction totals and who reacted. It lets a wave-1 server task test against these rows
// without another task's code. It is the one place a test writes them; rounds, options and answers go through PollRowSeed,
// and everything else comes from QaSeed and the API. Stamps come from the app's clock. Nothing here checks the Q&A rules:
// it writes what it is asked to, and the database's own keys, indexes and checks are the only refusals.
public sealed class QaRowSeed(IServiceProvider services)
{
    // The asker or voter when a test doesn't name one.
    public const string GUEST = "seed-guest";

    private PollRowSeed Polls => new(services);

    // An open Q&A round in the session, numbered after the session's earlier Q&A rounds. Makes the deck's Q&A poll first,
    // through Poll.ForQa, when the deck has none.
    public async Task<PollRound> RoundAsync(string deckId, Guid sessionId)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Polls.AnyAsync(p => p.DeckId == deckId && p.Kind == PollKind.Qa))
            {
                db.Polls.Add(Poll.ForQa(deckId));
                await db.SaveChangesAsync();
            }
        }
        return await Polls.QaRoundAsync(deckId, sessionId);
    }

    // The round's next question: its option (the text) and its question row at the same position, number Position + 1.
    // Hidden sets HiddenAt; an answered question gets AnsweredAt. The option is written first, so a question the database
    // refuses (a second on screen, a name over 30 characters) leaves its option behind.
    public async Task<Question> QuestionAsync(
        Guid roundId,
        string text,
        QuestionState state = QuestionState.Open,
        bool hidden = false,
        bool onScreen = false,
        string guestId = GUEST,
        string? name = null)
    {
        var option = await Polls.OptionAsync(roundId, text);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Now(scope);
        var question = new Question
        {
            RoundId = roundId,
            Position = option.Position,
            GuestId = guestId,
            Name = name,
            State = state,
            HiddenAt = hidden ? now : null,
            OnScreen = onScreen,
            AskedAt = now,
            AnsweredAt = state == QuestionState.Answered ? now : null,
        };
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return question;
    }

    // The guest's upvotes in the round, by 1-based question number: its one answer, whose picks are the positions. Replaces
    // what it had, revision one more each time.
    public Task<PollAnswer> VoteAsync(Guid roundId, string guestId, params int[] numbers)
        => Polls.AnswerAsync(roundId, guestId, [.. numbers.Select(number => number - 1)]);

    // Writes the deck's settings row, or replaces it.
    public async Task SettingsAsync(string deckId, QaSettings settings)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.QaSettingsRows.SingleOrDefaultAsync(s => s.DeckId == deckId);
        if (row == null)
        {
            db.QaSettingsRows.Add(QaSettingsRow.From(deckId, settings));
        }
        else
        {
            db.Entry(row).CurrentValues.SetValues(QaSettingsRow.From(deckId, settings));
        }
        await db.SaveChangesAsync();
    }

    // Writes the slide's switch row, or replaces it.
    public async Task SwitchAsync(Guid slideId, QaSlideSwitch slideSwitch)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.QaSlideSwitchRows.SingleOrDefaultAsync(s => s.SlideId == slideId);
        if (row == null)
        {
            db.QaSlideSwitchRows.Add(QaSlideSwitchRow.From(slideId, slideSwitch));
        }
        else
        {
            db.Entry(row).CurrentValues.SetValues(QaSlideSwitchRow.From(slideId, slideSwitch));
        }
        await db.SaveChangesAsync();
    }

    // Adds count taps of the emoji to the session's total for the slide, as a 5-second write does.
    public async Task<ReactionTotal> ReactionAsync(Guid sessionId, Guid slideId, ReactionEmoji emoji, int count = 1)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var total = await db.ReactionTotals.SingleOrDefaultAsync(t => t.SessionId == sessionId && t.SlideId == slideId && t.Emoji == emoji);
        if (total == null)
        {
            total = new ReactionTotal { SessionId = sessionId, SlideId = slideId, Emoji = emoji, Count = count };
            db.ReactionTotals.Add(total);
        }
        else
        {
            total.Count += count;
        }
        await db.SaveChangesAsync();
        return total;
    }

    // Records that the guest reacted in the session, unless it already is.
    public async Task ReactionGuestAsync(Guid sessionId, string guestId = GUEST)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.ReactionGuests.AnyAsync(g => g.SessionId == sessionId && g.GuestId == guestId))
        {
            return;
        }
        db.ReactionGuests.Add(new ReactionGuest { SessionId = sessionId, GuestId = guestId });
        await db.SaveChangesAsync();
    }

    // Runs one raw write, for a test that proves the database refuses a value EF can't write (an unknown enum name, say).
    // A Guid goes in as EF stores it: SqlGuid(id).
    public async Task SqlAsync(FormattableString sql)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(sql);
    }

    // A Guid as EF Core's SQLite provider stores it: upper-case text.
    public static string SqlGuid(Guid id) => id.ToString("D").ToUpperInvariant();

    private static DateTimeOffset Now(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
}
