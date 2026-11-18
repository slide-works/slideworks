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

// The presenter's seven actions on a question of the live session's open Q&A round, and the view they return (Q6, D13).
// Each action runs every check inside one write transaction before it writes anything (EF begins SQLite's transaction with
// BEGIN IMMEDIATE, so a second action waits and re-reads the row under the lock): not live is not_live, a number the open
// round has no question at (or no open round) is a 404, and a question in the wrong state is wrong_state. A refused action
// changes nothing. An accepted one writes the question and its activity entry (the action only, never the text) in that
// transaction; after the commit, and never inside it, it bumps the round's list revision, calls IAudienceRelay.RefreshAsync
// and then IQuestionPublisher.PublishAsync, one after the other, and answers the view after them.
//
// Show takes any other question off screen and saves that before it puts this one on, so the partial unique index on
// OnScreen and CK_Questions_OnScreen (on screen only while open and not hidden) hold after every statement. Hide and Answer
// take the question off screen in the same save. Show on the question already on screen succeeds and changes nothing: no
// write, no activity, no revision, no publish.
//
// Per request.
[RegisterPerRequest(For = typeof(IQuestionModeration))]
public class QuestionModeration(
    AppDbContext db,
    QaReader reader,
    QaRevisions revisions,
    IActivityLog activity,
    IAudienceRelay relay,
    IQuestionPublisher publisher,
    ICurrentActor actor,
    TimeProvider clock) : IQuestionModeration
{
    // Waiting → open. A hidden waiting question is restored first.
    public Task<QaControlView> ApproveAsync(string deckId, int number)
        => ActAsync(deckId, number, "approved a question", question =>
        {
            Require(question.State == QuestionState.Waiting && question.HiddenAt == null);
            question.State = QuestionState.Open;
            return Task.FromResult(true);
        });

    // From waiting or open, also while on screen, which takes it off. The state under it is kept.
    public Task<QaControlView> HideAsync(string deckId, int number)
        => ActAsync(deckId, number, "hid a question", question =>
        {
            Require(question.State != QuestionState.Answered && question.HiddenAt == null);
            question.HiddenAt = clock.GetUtcNow();
            question.OnScreen = false;
            return Task.FromResult(true);
        });

    // Clears the hidden flag: the question is back in the state it had.
    public Task<QaControlView> RestoreAsync(string deckId, int number)
        => ActAsync(deckId, number, "restored a question", question =>
        {
            Require(question.HiddenAt != null);
            question.HiddenAt = null;
            return Task.FromResult(true);
        });

    // Open → answered, off screen; its votes are frozen from now on (the vote writer keeps them).
    public Task<QaControlView> AnswerAsync(string deckId, int number)
        => ActAsync(deckId, number, "answered a question", question =>
        {
            Require(question.State == QuestionState.Open && question.HiddenAt == null);
            question.State = QuestionState.Answered;
            question.AnsweredAt = clock.GetUtcNow();
            question.OnScreen = false;
            return Task.FromResult(true);
        });

    // Answered → open; its votes can change again.
    public Task<QaControlView> ReopenAsync(string deckId, int number)
        => ActAsync(deckId, number, "reopened a question", question =>
        {
            Require(question.State == QuestionState.Answered && question.HiddenAt == null);
            question.State = QuestionState.Open;
            question.AnsweredAt = null;
            return Task.FromResult(true);
        });

    // An open, not hidden question goes on screen and replaces any other.
    public Task<QaControlView> ShowAsync(string deckId, int number)
        => ActAsync(deckId, number, "showed a question on screen", async question =>
        {
            Require(question.State == QuestionState.Open && question.HiddenAt == null);
            if (question.OnScreen)
            {
                return false;
            }
            var others = await db.Questions.Where(q => q.RoundId == question.RoundId && q.OnScreen).ToListAsync();
            foreach (var other in others)
            {
                other.OnScreen = false;
            }
            await db.SaveChangesAsync();
            question.OnScreen = true;
            return true;
        });

    // Takes the question off screen; it stays open.
    public Task<QaControlView> UnshowAsync(string deckId, int number)
        => ActAsync(deckId, number, "took a question off screen", question =>
        {
            Require(question.OnScreen);
            question.OnScreen = false;
            return Task.FromResult(true);
        });

    public Task<QaControlView> ControlAsync(string deckId) => reader.ControlAsync(deckId);

    // Every check, then change (which checks the state first and answers false when there is nothing to change), then the
    // save and the activity entry, then the commit; after it the revision, the refresh and the publish, then the view.
    private async Task<QaControlView> ActAsync(string deckId, int number, string summary, Func<Question, Task<bool>> change)
    {
        Guid round;
        bool changed;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var session = await reader.LiveSessionAsync(deckId) ?? throw new ConflictException(QaRefusals.NOT_LIVE);
            round = await reader.OpenRoundAsync(session) ?? throw new NotFoundException(QaRefusals.NO_QUESTION);
            var question = await db.Questions.SingleOrDefaultAsync(q => q.RoundId == round && q.Position == number - 1)
                ?? throw new NotFoundException(QaRefusals.NO_QUESTION);
            changed = await change(question);
            if (changed)
            {
                await db.SaveChangesAsync();
                await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool: null, summary));
                await transaction.CommitAsync();
            }
        }
        if (changed)
        {
            revisions.Bump(round.ToString());
            await relay.RefreshAsync(deckId);
            await publisher.PublishAsync(deckId);
        }
        return await reader.ControlAsync(deckId);
    }

    private static void Require(bool allowed)
    {
        if (!allowed)
        {
            throw new ConflictException(QaRefusals.WRONG_STATE);
        }
    }
}
