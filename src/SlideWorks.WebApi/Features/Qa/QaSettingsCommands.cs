using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// A deck's Q&A settings and its per-slide switches (Q1, Q6, D16): the real IQaSettings. Live or not, for whoever the caller
// (QaAccess, or update_qa) has already let in.
// - A change reads the row, applies only the fields given and writes it in one write transaction, with one activity entry
//   naming what changed ("turned review on"; never a question). EF begins a SQLite transaction with BEGIN IMMEDIATE, so two
//   first changes can't both add the row. A patch that changes nothing writes, logs and publishes nothing.
// - The settings row is made on the first change and changed in place after it. A slide's switch row is made only when
//   something on the slide is turned off, and changed in place after it: turning both back on stores true, never deletes.
//   A switch for something the deck already has off is stored as given; QaRoomRules makes the deck's off win.
// - After the commit, and never inside it: questions leaving off calls IQaRounds.EnsureAsync (which opens the live
//   session's round if it has none, and does nothing when not live), then IAudienceRelay.RefreshAsync, then
//   IQuestionPublisher.PublishAsync. Questions going off leaves the round and its questions as they are; the room just has
//   no round. Review going on leaves questions already open as they are.
// Per request.
[RegisterPerRequest(For = typeof(IQaSettings))]
public sealed class QaSettingsCommands(
    AppDbContext db,
    QaRows rows,
    IQaRounds rounds,
    IActivityLog activity,
    IAudienceRelay relay,
    IQuestionPublisher publisher,
    ICurrentActor actor) : IQaSettings
{
    // The MCP tool an agent changes these through, named in its activity entries.
    public const string TOOL = "update_qa";
    public const string SLIDE_NOT_FOUND = "Slide not found";

    public Task<QaSettings> GetAsync(string deckId) => rows.SettingsAsync(deckId);

    public async Task<QaSettings> UpdateAsync(string deckId, QaSettingsPatch patch)
    {
        QaSettings before;
        QaSettings after;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var row = await db.QaSettingsRows.SingleOrDefaultAsync(s => s.DeckId == deckId);
            before = row?.ToSettings() ?? QaSettings.Default;
            after = new QaSettings(
                patch.Questions ?? before.Questions,
                patch.Review ?? before.Review,
                patch.Names ?? before.Names,
                patch.Reactions ?? before.Reactions,
                patch.ShowOn ?? before.ShowOn);
            if (after == before)
            {
                return after;
            }
            if (row == null)
            {
                db.QaSettingsRows.Add(QaSettingsRow.From(deckId, after));
            }
            else
            {
                db.Entry(row).CurrentValues.SetValues(QaSettingsRow.From(deckId, after));
            }
            await db.SaveChangesAsync();
            await RecordAsync(deckId, Summary(before, after));
            await transaction.CommitAsync();
        }
        if (before.Questions == QaQuestions.Off)
        {
            await rounds.EnsureAsync(deckId);
        }
        await AnnounceAsync(deckId);
        return after;
    }

    public async Task<QaSlideSwitch> SetSlideAsync(string deckId, int slide, QaSlidePatch patch)
    {
        QaSlideSwitch after;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var slideId = await db.Slides.AsNoTracking()
                .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1)
                .Select(s => (Guid?)s.Id)
                .SingleOrDefaultAsync() ?? throw new NotFoundException(SLIDE_NOT_FOUND);
            var row = await db.QaSlideSwitchRows.SingleOrDefaultAsync(s => s.SlideId == slideId);
            var before = row?.ToSwitch() ?? QaSlideSwitch.On;
            after = new QaSlideSwitch(patch.Questions ?? before.Questions, patch.Reactions ?? before.Reactions);
            if (after == before)
            {
                return after;
            }
            if (row == null)
            {
                db.QaSlideSwitchRows.Add(QaSlideSwitchRow.From(slideId, after));
            }
            else
            {
                db.Entry(row).CurrentValues.SetValues(QaSlideSwitchRow.From(slideId, after));
            }
            await db.SaveChangesAsync();
            await RecordAsync(deckId, Summary(slide, before, after));
            await transaction.CommitAsync();
        }
        await AnnounceAsync(deckId);
        return after;
    }

    private Task RecordAsync(string deckId, string summary)
        => activity.RecordAsync(deckId, ActivityEntry.By(actor, actor.Via == ActorVia.Mcp ? TOOL : null, summary));

    // After the commit: the audience state (its room), then the Q&A events, one after the other.
    private async Task AnnounceAsync(string deckId)
    {
        await relay.RefreshAsync(deckId);
        await publisher.PublishAsync(deckId);
    }

    // What changed, such as "turned review on, closed questions".
    private static string Summary(QaSettings before, QaSettings after)
    {
        var parts = new List<string>();
        if (after.Questions != before.Questions)
        {
            parts.Add(after.Questions switch
            {
                QaQuestions.Off => "turned questions off",
                QaQuestions.Closed => "closed questions",
                _ => before.Questions == QaQuestions.Off ? "turned questions on" : "opened questions",
            });
        }
        if (after.Review != before.Review)
        {
            parts.Add($"turned review {OnOff(after.Review)}");
        }
        if (after.Names != before.Names)
        {
            parts.Add($"turned names {OnOff(after.Names)}");
        }
        if (after.Reactions != before.Reactions)
        {
            parts.Add($"turned reactions {OnOff(after.Reactions)}");
        }
        if (after.ShowOn != before.ShowOn)
        {
            parts.Add(after.ShowOn == QaShowOn.QaSlide ? "set shown questions to the Q&A slide only" : "set shown questions to any slide");
        }
        return string.Join(", ", parts);
    }

    // What changed on the slide, such as "turned questions off on slide 3".
    private static string Summary(int slide, QaSlideSwitch before, QaSlideSwitch after)
    {
        var parts = new List<string>();
        if (after.Questions != before.Questions)
        {
            parts.Add($"questions {OnOff(after.Questions)}");
        }
        if (after.Reactions != before.Reactions)
        {
            parts.Add($"reactions {OnOff(after.Reactions)}");
        }
        return $"turned {string.Join(" and ", parts)} on slide {slide}";
    }

    private static string OnOff(bool on) => on ? "on" : "off";
}
