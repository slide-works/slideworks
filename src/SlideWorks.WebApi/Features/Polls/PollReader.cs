using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// The presenter's view of a poll slide (V4), read from the poll's rows in the deck's live audience session. The current
// round is the session's unclosed round on the slide (draft or open), else the last round that was opened (closed), else
// none (draft). A row with ClosedAt and no OpenedAt is a discarded draft: never a closed round, but its number is used up.
// An open round's Answered and Counts come from IPollTallies; a closed round's are its Final snapshot, with the option
// texts it ran with. Seq comes from PollSeq. Per request, because it uses the database.
[RegisterPerRequest]
public class PollReader(AppDbContext db, PollRows rows, IAudienceSessions sessions, IAudienceRoster roster, IPollTallies tallies, PollSeq seq)
{
    public async Task<PollControlView?> ControlAsync(string deckId, int slide)
    {
        if (await SlideAsync(deckId, slide) is not { } found)
        {
            return null;
        }
        var (row, settings) = found;
        var (question, options) = SlotsOf(row.SlotsJson);
        var session = await LiveSessionAsync(deckId);
        var rounds = session == null ? [] : await RoundsAsync(row.Id, session.Value, tracked: false);
        var joined = session == null ? 0 : await roster.CountAsync(session.Value.ToString());
        var closed = rounds.Where(IsClosed).ToList();
        var summaries = closed.Select(r => new PollRoundSummary(r.Number, r.Final!.Answered, r.JoinedAtClose ?? 0)).ToList();
        var unclosed = rounds.SingleOrDefault(r => r.ClosedAt == null);

        if (unclosed is { OpenedAt: not null })
        {
            var tally = await tallies.ReadAsync(unclosed.Id.ToString(), options.Count);
            var revealed = unclosed.RevealedAt != null;
            return new PollControlView(
                seq.Next(), slide, unclosed.Id.ToString(), unclosed.Number, PollPhase.Open, settings, question, options,
                tally.Answered, joined, tally.Counts, PollVisibility.RoomSees(settings.Results, revealed, closed: false), revealed, false, summaries);
        }
        if (unclosed == null && closed.Count > 0)
        {
            var last = closed[^1];
            var final = last.Final!;
            var revealed = last.RevealedAt != null;
            return new PollControlView(
                seq.Next(), slide, last.Id.ToString(), last.Number, PollPhase.Closed, settings, question, final.Options,
                final.Answered, last.JoinedAtClose ?? 0, final.Counts, PollVisibility.RoomSees(settings.Results, revealed, closed: true), revealed,
                last.AnswerShownAt != null, summaries);
        }
        var number = unclosed?.Number ?? NextNumber(rounds);
        return new PollControlView(
            seq.Next(), slide, null, number, PollPhase.Draft, settings, question, options,
            0, joined, new int[options.Count], PollVisibility.RoomSees(settings.Results, revealed: false, closed: false), false, false, summaries);
    }

    // The deck's live poll slide at a 1-based number, with its settings, or null when that slide is not a poll.
    public async Task<(Slide Slide, PollSettings Settings)?> SlideAsync(string deckId, int slide)
    {
        var row = await db.Slides.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1);
        if (row?.Layout != PollRows.LAYOUT)
        {
            return null;
        }
        var settings = await rows.SettingsAsync(row.Id) ?? throw new InvalidOperationException($"Poll slide {row.Id} has no settings row.");
        return (row, settings);
    }

    // The deck's open audience session, or null when it isn't live. The phase rule is IAudienceSessions'.
    public async Task<Guid?> LiveSessionAsync(string deckId)
        => await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live, SessionId: { } id } ? Guid.Parse(id) : null;

    // Every round of the slide in the session, discarded drafts included, by number.
    public async Task<IReadOnlyList<PollRound>> RoundsAsync(Guid slideId, Guid sessionId, bool tracked)
    {
        var query = db.PollRounds.Where(r => r.SlideId == slideId && r.SessionId == sessionId);
        return await (tracked ? query : query.AsNoTracking()).OrderBy(r => r.Number).ToListAsync();
    }

    // A round that was opened and then closed, as opposed to a draft that was discarded.
    public static bool IsClosed(PollRound round) => round.OpenedAt != null && round.ClosedAt != null;

    // The number the slide's next round gets: one more than the highest, discarded drafts included, so theirs stay used up.
    public static int NextNumber(IReadOnlyList<PollRound> rounds) => rounds.Count == 0 ? 1 : rounds.Max(r => r.Number) + 1;

    // The poll layout's slots: the question and the option texts, by position.
    public static (string Question, IReadOnlyList<string> Options) SlotsOf(string slotsJson)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        var question = slots.RootElement.GetProperty("question").GetString() ?? "";
        var options = slots.RootElement.GetProperty("options").EnumerateArray().Select(o => o.GetString() ?? "").ToList();
        return (question, options);
    }
}
