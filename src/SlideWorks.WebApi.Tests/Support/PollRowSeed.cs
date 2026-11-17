using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Writes poll rows straight to the database, for tests only: a poll's settings, a round in any phase, a Q&A round and its
// options (SW-9 Q2), and guests' answers. It lets a server task test against rounds and answers without another task's
// code. It is the one place a test writes poll rows, and the one exception to seeding through the API; everything else
// comes from PollSeed. Stamps come from the app's clock. A closed round's Final is counted from its answer rows, as
// closing a round does (V4).
public sealed class PollRowSeed(IServiceProvider services)
{
    // Replaces the slide's settings (the row PollSeed's poll slide already has).
    public async Task SettingsAsync(Guid slideId, PollSettings settings)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var poll = await db.Polls.SingleAsync(p => p.SlideId == slideId);
        var next = Poll.From(slideId, poll.DeckId, settings);
        next.Id = poll.Id;
        db.Entry(poll).CurrentValues.SetValues(next);
        await db.SaveChangesAsync();
    }

    // A draft or open round, revealed when asked. A closed round is an open one passed to CloseAsync after its answers.
    public async Task<PollRound> RoundAsync(Guid slideId, Guid sessionId, PollPhase phase = PollPhase.Open, int number = 1, bool revealed = false)
    {
        if (phase == PollPhase.Closed)
        {
            throw new ArgumentException("Seed an open round, its answers, then CloseAsync.", nameof(phase));
        }
        if (revealed && phase != PollPhase.Open)
        {
            throw new ArgumentException("Only an open round is revealed.", nameof(revealed));
        }
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Now(scope);
        var round = new PollRound
        {
            Id = Guid.NewGuid(),
            PollId = await SlidePollIdAsync(db, slideId),
            Kind = PollKind.Slide,
            SlideId = slideId,
            SessionId = sessionId,
            Number = number,
            OpenedAt = phase == PollPhase.Open ? now : null,
            RevealedAt = revealed ? now : null,
        };
        db.PollRounds.Add(round);
        await db.SaveChangesAsync();
        return round;
    }

    // An open Q&A round (SW-9 Q2) in the session, numbered one after the deck's Q&A poll's last round in it. Makes the
    // deck's Q&A poll first when it has none (Pick Any, no slide), as the Q&A rounds service does.
    public async Task<PollRound> QaRoundAsync(string deckId, Guid sessionId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var poll = await db.Polls.SingleOrDefaultAsync(p => p.DeckId == deckId && p.Kind == PollKind.Qa);
        if (poll == null)
        {
            var settings = new PollSettings(PollPick.Any, null, PollResults.Live, null, PollOpens.Manual, PollPrivacy.Private);
            poll = new Poll
            {
                Id = Guid.NewGuid(),
                Kind = PollKind.Qa,
                SlideId = null,
                DeckId = deckId,
                Pick = settings.Pick,
                Max = settings.Max,
                Results = settings.Results,
                Correct = settings.Correct,
                Opens = settings.Opens,
                Privacy = settings.Privacy,
            };
            db.Polls.Add(poll);
        }
        var numbers = await db.PollRounds.Where(r => r.PollId == poll.Id && r.SessionId == sessionId).Select(r => r.Number).ToListAsync();
        var round = new PollRound
        {
            Id = Guid.NewGuid(),
            PollId = poll.Id,
            Kind = PollKind.Qa,
            SlideId = null,
            SessionId = sessionId,
            Number = numbers.Count == 0 ? 1 : numbers.Max() + 1,
            OpenedAt = Now(scope),
        };
        db.PollRounds.Add(round);
        await db.SaveChangesAsync();
        return round;
    }

    // The next option of a Q&A round: a question's text at the next position (0-based).
    public async Task<PollOption> OptionAsync(Guid roundId, string text)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var option = new PollOption
        {
            RoundId = roundId,
            Position = await db.PollOptions.CountAsync(o => o.RoundId == roundId),
            Text = text,
            CreatedAt = Now(scope),
        };
        db.PollOptions.Add(option);
        await db.SaveChangesAsync();
        return option;
    }

    // A guest's answer: revision 1 the first time, one more on each replace.
    public async Task<PollAnswer> AnswerAsync(Guid roundId, string guestId, params int[] picks)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sorted = picks.Order().ToArray();
        var answer = await db.PollAnswers.SingleOrDefaultAsync(a => a.RoundId == roundId && a.GuestId == guestId);
        if (answer == null)
        {
            answer = new PollAnswer { RoundId = roundId, GuestId = guestId, Picks = sorted, Revision = 1, UpdatedAt = Now(scope) };
            db.PollAnswers.Add(answer);
        }
        else
        {
            answer.Picks = sorted;
            answer.Revision++;
            answer.UpdatedAt = Now(scope);
        }
        await db.SaveChangesAsync();
        return answer;
    }

    // Closes the round: ClosedAt, JoinedAtClose and Final (the slide's option texts now, the count per option and how many
    // answered, from the answer rows). With answerShown, the answer is shown too. It counts the rows and writes the close in
    // one write transaction (BEGIN IMMEDIATE), as Polls.CloseAsync does, so an answer that commits around it is either in
    // Final or refused as stale by the answer writer.
    public async Task<PollRound> CloseAsync(Guid roundId, int joinedAtClose, bool answerShown = false)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var round = await db.PollRounds.SingleAsync(r => r.Id == roundId);
        var options = round.Kind == PollKind.Qa
            ? await db.PollOptions.AsNoTracking().Where(o => o.RoundId == roundId).OrderBy(o => o.Position).Select(o => o.Text).ToListAsync()
            : await SlideOptionsAsync(db, round.SlideId);
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == roundId).Select(a => a.Picks).ToListAsync();
        var counts = options.Select((_, position) => answers.Count(picks => picks.Contains(position))).ToList();

        var now = Now(scope);
        round.ClosedAt = now;
        round.JoinedAtClose = joinedAtClose;
        round.Final = new PollFinal(options, counts, answers.Count);
        round.AnswerShownAt = answerShown ? now : null;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return round;
    }

    // The slide's poll id. A slide that has never been a poll gets a settings row with the defaults first, so a round can
    // be seeded on any slide, as before SW-9 gave rounds a poll.
    private static async Task<Guid> SlidePollIdAsync(AppDbContext db, Guid slideId)
    {
        if (await db.Polls.Where(p => p.SlideId == slideId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync() is { } id)
        {
            return id;
        }
        var deckId = await db.Slides.AsNoTracking().Where(s => s.Id == slideId).Select(s => s.DeckId).SingleAsync();
        var poll = Poll.From(slideId, deckId, PollSettings.Default);
        db.Polls.Add(poll);
        return poll.Id;
    }

    private static async Task<List<string>> SlideOptionsAsync(AppDbContext db, Guid? slideId)
    {
        var slots = await db.Slides.AsNoTracking().Where(s => s.Id == slideId).Select(s => s.SlotsJson).SingleAsync();
        using var parsed = JsonDocument.Parse(slots);
        return parsed.RootElement.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToList();
    }

    private static DateTimeOffset Now(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
}
