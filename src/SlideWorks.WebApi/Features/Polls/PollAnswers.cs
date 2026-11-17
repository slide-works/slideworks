using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;

namespace SlideWorks.WebApi.Features.Polls;

// A guest's answers (V5). Every check runs before anything is queued, in this order: no session for the code, or a guest
// not on the live session's roster, is NoSession before anything else, so a guessed code or guest id learns nothing; then
// the guest's 30 a minute (D18); then stale (no open round, another round, another revision); then the picks' rules
// (AnswerRules). An answer that passes goes to the batching writer (IRoundAnswerWriter), which checks the round and the revision again
// inside its commit, so an answer to a round that closed meanwhile comes back stale too, never as a 400. A known code that
// isn't live has no poll: its view is empty and an answer to it is stale.
[RegisterPerRequest(For = typeof(IPollAnswers))]
public class PollAnswers(AppDbContext db, GuestPollViews views, PollRows rows, AnswerLimiter limiter, IRoundAnswerWriter writer) : IPollAnswers
{
    private const string OPTIONS_SLOT = "options";

    public async Task<AnswerResult> SubmitAsync(string code, string guestId, AnswerRequest request)
    {
        var seat = await views.SeatAsync(code, guestId);
        if (seat is null)
        {
            return new AnswerResult.NoSession();
        }
        if (seat.SessionId is not { } sessionId)
        {
            return await StaleAsync(seat, code, guestId);
        }
        if (!limiter.TryAcquire(guestId))
        {
            return new AnswerResult.Limited();
        }
        var round = await rows.OpenRoundAsync(sessionId);
        if (round?.OpenedAt is null || !Guid.TryParse(request.Round, out var asked) || asked != round.Id)
        {
            return await StaleAsync(seat, code, guestId);
        }
        var mine = await views.MineAsync(round.Id, guestId);
        if ((mine?.Revision ?? 0) != request.Revision)
        {
            return await StaleAsync(seat, code, guestId);
        }
        var settings = await rows.RoundSettingsAsync(round);
        var slide = await db.Slides.AsNoTracking()
            .Where(s => s.Id == round.SlideId)
            .Select(s => new { s.DeckId, s.Position, s.SlotsJson })
            .SingleAsync();
        var optionCount = OptionCountOf(slide.SlotsJson);
        if (AnswerRules.Check(request.Picks, settings, optionCount) is { } broken)
        {
            return new AnswerResult.Invalid(broken);
        }

        var target = new PollRoundRef(slide.DeckId, PollKind.Slide, slide.Position + 1, round.SlideId, sessionId.ToString(), round.Id.ToString(), optionCount);
        return await writer.ReplaceAsync(target, guestId, [.. request.Picks.Order()], request.Revision) is RoundWrite.Written written
            ? new AnswerResult.Accepted(written.Picks, written.Revision)
            : await StaleAsync(seat, code, guestId);
    }

    public async Task<GuestPollView?> ViewAsync(string code, string guestId)
    {
        var seat = await views.SeatAsync(code, guestId);
        return seat is null ? null : await views.ViewAsync(seat, code, guestId);
    }

    public Task<MyAnswer?> MineAsync(string roundId, string guestId)
        => Guid.TryParse(roundId, out var id) ? views.MineAsync(id, guestId) : Task.FromResult<MyAnswer?>(null);

    public async Task<string?> CanWatchAsync(string code, string guestId)
    {
        if (await views.SeatAsync(code, guestId) is not { SessionId: { } sessionId })
        {
            return null;
        }
        var round = await rows.OpenRoundAsync(sessionId);
        if (round?.OpenedAt is null || (await rows.RoundSettingsAsync(round)).Results != PollResults.AfterAnswer)
        {
            return null;
        }
        return await views.MineAsync(round.Id, guestId) is null ? null : round.Id.ToString();
    }

    private async Task<AnswerResult> StaleAsync(GuestSeat seat, string code, string guestId)
        => new AnswerResult.Stale(await views.ViewAsync(seat, code, guestId));

    // The poll slide's option count, from its options slot (V1).
    private static int OptionCountOf(string slotsJson)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        return slots.RootElement.GetProperty(OPTIONS_SLOT).GetArrayLength();
    }
}
