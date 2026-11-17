using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Qa.Data;

// The Q&A reads every Q&A service shares (Q1, Q2). Reads only: each service writes its own rows. Each runs on the request's
// context, so a call inside a caller's transaction is part of it. Nothing read here is tracked.
[RegisterPerRequest]
public class QaRows(AppDbContext db)
{
    // Every question of the round, in position (number) order.
    public async Task<IReadOnlyList<Question>> QuestionsAsync(Guid roundId)
        => await db.Questions.AsNoTracking().Where(q => q.RoundId == roundId).OrderBy(q => q.Position).ToListAsync();

    // The question with this 1-based number in the round, or null when the round has no such question.
    public Task<Question?> QuestionAsync(Guid roundId, int number)
        => db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.RoundId == roundId && q.Position == number - 1);

    // The deck's settings, or QaSettings.Default when the deck has no settings row.
    public async Task<QaSettings> SettingsAsync(string deckId)
        => (await db.QaSettingsRows.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId))?.ToSettings() ?? QaSettings.Default;

    // The slide's switch, or QaSlideSwitch.On when the slide has no switch row.
    public async Task<QaSlideSwitch> SwitchAsync(Guid slideId)
        => (await db.QaSlideSwitchRows.AsNoTracking().SingleOrDefaultAsync(s => s.SlideId == slideId))?.ToSwitch() ?? QaSlideSwitch.On;

    // The switches of the deck's slides that have something off, by 1-based slide number among the deck's live slides. A
    // deleted slide, and a slide whose row has both on again, isn't listed.
    public async Task<IReadOnlyDictionary<int, QaSlideSwitch>> SwitchesAsync(string deckId)
    {
        var rows = await db.QaSlideSwitchRows.AsNoTracking()
            .Where(s => !s.Questions || !s.Reactions)
            .Join(
                db.Slides.AsNoTracking().Where(slide => slide.DeckId == deckId && slide.DeletedAt == null),
                s => s.SlideId,
                slide => slide.Id,
                (s, slide) => new { slide.Position, s.Questions, s.Reactions })
            .ToListAsync();
        return rows.ToDictionary(r => r.Position + 1, r => new QaSlideSwitch(r.Questions, r.Reactions));
    }
}
