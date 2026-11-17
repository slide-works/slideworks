using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// What one guest's phone gets for a join code (V5, guest-poll.json): the activity on screen, which only IAudienceActivity
// builds; this guest's own answer in its round; and the round's tally, with counts only when PollVisibility.GuestSees says
// this guest may see them. Answered always goes (D21). An open round's tally is IPollTallies'; a closed round's is its Final.
// Nothing here reads another guest's answer.
[RegisterPerRequest]
public class GuestPollViews(AppDbContext db, IAudienceSessions sessions, IAudienceActivity activities, IPollTallies tallies, PollRows rows)
{
    private static readonly GuestPollView _nothing = new(null, null, null, false);

    // The guest's seat for a code, or null when no session has the code or, while it is live, this guest is not on the live
    // session's roster (has never joined it): either way the guest is told no_session. A known code that isn't live gives a
    // seat with no session.
    public async Task<GuestSeat?> SeatAsync(string code, string guestId)
    {
        var found = await sessions.FindAsync(code);
        if (found is null)
        {
            return null;
        }
        if (found.SessionId is null)
        {
            return new GuestSeat(found.DeckId, null, null);
        }
        var sessionId = Guid.Parse(found.SessionId);
        var joinedAt = await db.AudienceJoins.AsNoTracking()
            .Where(j => j.SessionId == sessionId && j.GuestId == guestId)
            .Select(j => (DateTimeOffset?)j.JoinedAt)
            .SingleOrDefaultAsync();
        return joinedAt is null ? null : new GuestSeat(found.DeckId, sessionId, joinedAt);
    }

    // The view for a seated guest. Every read is untracked, so a view built after a refused answer sees the rows as they are
    // now, not as this request first read them.
    public async Task<GuestPollView> ViewAsync(GuestSeat seat, string code, string guestId)
    {
        if (seat.SessionId is not { } sessionId)
        {
            return _nothing;
        }
        var activity = await activities.ForCodeAsync(code);
        if (activity?.Round is null || !Guid.TryParse(activity.Round, out var roundId))
        {
            return activity is null ? _nothing : new GuestPollView(activity, null, null, false);
        }
        var round = await db.PollRounds.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roundId && r.SessionId == sessionId);
        if (round?.OpenedAt is null)
        {
            return new GuestPollView(activity, null, null, false);
        }
        var mine = await MineAsync(roundId, guestId);
        var results = (await rows.SettingsAsync(round.SlideId)
            ?? throw new InvalidOperationException($"Round {round.Id} has no poll settings for slide {round.SlideId}.")).Results;
        var closed = round.ClosedAt is not null;
        var counts = closed ? FinalOf(round) : await tallies.ReadAsync(activity.Round, activity.Options.Count);
        var sees = PollVisibility.GuestSees(PollVisibility.RoomSees(results, round.RevealedAt is not null, closed), results, mine is not null);
        var tally = new PollTally(seat.DeckId, PollTallyTarget.Audience, activity.Round, counts.Answered, sees ? counts.Counts : null);
        var joinedLate = closed && mine is null && seat.JoinedAt > round.ClosedAt;
        return new GuestPollView(activity, mine, tally, joinedLate);
    }

    // The guest's own answer in the round, or null.
    public async Task<MyAnswer?> MineAsync(Guid roundId, string guestId)
    {
        var answer = await db.PollAnswers.AsNoTracking().SingleOrDefaultAsync(a => a.RoundId == roundId && a.GuestId == guestId);
        return answer is null ? null : new MyAnswer(answer.Picks, answer.Revision);
    }

    private static PollCounts FinalOf(PollRound round)
    {
        var final = round.Final ?? throw new InvalidOperationException($"Closed round {round.Id} has no final result.");
        return new PollCounts(final.Answered, final.Counts);
    }
}
