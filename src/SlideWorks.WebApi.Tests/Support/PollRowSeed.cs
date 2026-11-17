using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Writes poll rows straight to the database, for tests only: a poll's settings, a round in any phase and guests'
// answers. It lets a server task test against rounds and answers without another task's code. It is the one place a
// test writes poll rows, and the one exception to seeding through the API; everything else comes from PollSeed. Stamps
// come from the app's clock. A closed round's Final is counted from its answer rows, as closing a round does (V4).
public sealed class PollRowSeed(IServiceProvider services)
{
    // Replaces the slide's settings (the row PollSeed's poll slide already has).
    public async Task SettingsAsync(Guid slideId, PollSettings settings)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var poll = await db.Polls.SingleAsync(p => p.SlideId == slideId);
        var next = Poll.From(slideId, poll.DeckId, settings);
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
    // answered, from the answer rows). With answerShown, the answer is shown too.
    public async Task<PollRound> CloseAsync(Guid roundId, int joinedAtClose, bool answerShown = false)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var round = await db.PollRounds.SingleAsync(r => r.Id == roundId);
        var slots = await db.Slides.AsNoTracking().Where(s => s.Id == round.SlideId).Select(s => s.SlotsJson).SingleAsync();
        using var parsed = JsonDocument.Parse(slots);
        var options = parsed.RootElement.GetProperty("options").EnumerateArray().Select(o => o.GetString()!).ToList();
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == roundId).Select(a => a.Picks).ToListAsync();
        var counts = options.Select((_, position) => answers.Count(picks => picks.Contains(position))).ToList();

        var now = Now(scope);
        round.ClosedAt = now;
        round.JoinedAtClose = joinedAtClose;
        round.Final = new PollFinal(options, counts, answers.Count);
        round.AnswerShownAt = answerShown ? now : null;
        await db.SaveChangesAsync();
        return round;
    }

    private static DateTimeOffset Now(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
}
