using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;

namespace SlideWorks.WebApi.Features.Qa;

// What one guest's phone gets for the Q&A round (Q4, guest-questions.json): the room from IAudienceRoom, whose round is the
// open Q&A round; every open and answered question that isn't hidden, plus this guest's own in any state (its status from
// QuestionStatuses, so hidden wins); each question's votes from IPollTallies, sized by the round's PollOption rows; and the
// guest's own upvotes from its answer row. Another guest's hidden or waiting question is left out here, never in the client,
// and nothing says which guest asked or upvoted but Yours on the guest's own question. Every read is untracked, so a view
// built after a refused vote sees the rows as they are now.
[RegisterPerRequest]
public class GuestQuestionViews(AppDbContext db, QaRows rows, IAudienceRoom rooms, IPollTallies tallies, TimeProvider clock)
{
    private static readonly MyVotes _noVotes = new([], 0);

    // The view of the deck's live session for this guest, or null when the deck has no room (it isn't live). With no round
    // (questions off) it has the room and no questions.
    public async Task<GuestQuestionsView?> ViewAsync(string deckId, string guestId)
    {
        if (await rooms.ForDeckAsync(deckId) is not { } room)
        {
            return null;
        }
        if (!Guid.TryParse(room.Round, out var roundId))
        {
            return new GuestQuestionsView(room, [], _noVotes);
        }
        var texts = await db.PollOptions.AsNoTracking()
            .Where(o => o.RoundId == roundId)
            .ToDictionaryAsync(o => o.Position, o => o.Text);
        var tally = await tallies.ReadAsync(roundId.ToString(), texts.Count);
        var now = clock.GetUtcNow();
        var questions = (await rows.QuestionsAsync(roundId))
            .Where(q => q.GuestId == guestId || (q.HiddenAt is null && q.State != QuestionState.Waiting))
            .Select(q => new GuestQuestion(
                q.Position + 1,
                texts[q.Position],
                q.Name,
                QuestionAge.Label(q.AskedAt, now),
                QuestionStatuses.Of(q.State, q.HiddenAt is not null),
                tally.Counts[q.Position],
                q.OnScreen,
                q.GuestId == guestId))
            .ToList();
        return new GuestQuestionsView(room, questions, MineOf(await AnswerAsync(roundId, guestId)));
    }

    // The guest's stored answer in the round (its upvotes as positions), or null before its first vote.
    public Task<PollAnswer?> AnswerAsync(Guid roundId, string guestId)
        => db.PollAnswers.AsNoTracking().SingleOrDefaultAsync(a => a.RoundId == roundId && a.GuestId == guestId);

    // How many options (questions) the round has, which sizes its tally.
    public Task<int> OptionCountAsync(Guid roundId) => db.PollOptions.AsNoTracking().CountAsync(o => o.RoundId == roundId);

    private static MyVotes MineOf(PollAnswer? answer)
        => answer is null ? _noVotes : new MyVotes([.. answer.Picks.Select(position => position + 1)], answer.Revision);
}
