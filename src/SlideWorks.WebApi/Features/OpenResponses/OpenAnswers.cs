using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's view of an open-text slide and their one answer (open-text.json, D13). A code with no live session is 404
// no_session. The view only needs that session: a phone with no cookie still reads the round, and the shared GET sets one.
// A submit with no cookie, or from a guest who has not joined, is the same 404, so a guessed code and a guessed guest
// learn the same thing. A submit is then counted by OpenResponseLimiter, refused while the round is not the open one the
// phone saw, and refused by OpenTextRules, before anything is written. One row per guest, at position 0. Its wall number
// is 1-based in arrival order (OpenEntry.Arrival, which an update leaves alone), so sending again keeps the number.
[RegisterPerRequest]
public class OpenAnswers(AppDbContext db, IAudienceSessions sessions, OpenResponseLimiter limiter, IOpenTextPublisher publisher)
{
    private const string LAYOUT = "open-text";

    // Whether this slide is the open-text one the guest GET should read here. A code with no live session is left to the
    // word-cloud GET, which answers no_session for either layout.
    public async Task<bool> IsOpenTextAsync(string code, int slide, CancellationToken cancellationToken)
    {
        var found = await sessions.FindAsync(code);
        if (found is not { Phase: AudiencePhase.Live, SessionId: not null })
        {
            return false;
        }
        var layout = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == found.DeckId && s.DeletedAt == null && s.Position == slide - 1)
            .Select(s => s.Layout)
            .SingleOrDefaultAsync(cancellationToken);
        return layout == LAYOUT;
    }

    public async Task<GuestResponseView> ViewAsync(string code, int slide, string? guestId, CancellationToken cancellationToken)
    {
        var seated = await LiveAsync(code);
        return ViewOf(await RoundAsync(seated, slide, cancellationToken), guestId);
    }

    public async Task<StoredText> SubmitAsync(string code, int slide, string guestId, TextRequest request, CancellationToken cancellationToken)
    {
        var seated = await SeatAsync(code, guestId, cancellationToken);
        limiter.Take(guestId, code, slide);
        var round = await RoundAsync(seated, slide, cancellationToken);
        OpenRoundChecks.RequireCurrent(ViewOf(round, guestId), request.Round, request.Revision);
        OpenRoundChecks.RequireValid(OpenTextRules.Check(request.Text));
        var text = OpenTextRules.Collapse(request.Text);
        if (round.RoundId is not { } roundId)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }
        await StoreAsync(roundId, guestId, text, request.Revision, cancellationToken);
        await publisher.PublishAsync(seated.DeckId, slide, cancellationToken);
        return new StoredText(text);
    }

    // The audience copy leaves hidden answers out. The presenter copy keeps every answer and its hidden flag. Revealed
    // stays true: a round opens already showing its answers (D14), and there is no reveal. Null when the deck is no
    // longer live, or the slide is no longer open-text: ending the session and changing the layout publish nothing.
    public async Task<(OpenTextTally Audience, OpenTextTally Presenter)?> TalliesAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        var found = await sessions.ForDeckAsync(deckId);
        if (found is not { Phase: AudiencePhase.Live, SessionId: not null })
        {
            return null;
        }
        var layout = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1)
            .Select(s => s.Layout)
            .SingleOrDefaultAsync(cancellationToken);
        if (layout != LAYOUT)
        {
            return null;
        }
        var round = await RoundAsync(new Seat(deckId, Guid.Parse(found.SessionId)), slide, cancellationToken);
        var answers = AnswersOf(round);
        WallAnswer[] audience = [.. answers.Where(answer => !answer.Hidden).Select(answer => answer with { Hidden = false })];
        return (Tally(deckId, slide, round, OpenResponseTarget.Audience, audience), Tally(deckId, slide, round, OpenResponseTarget.Presenter, answers));
    }

    private async Task<Seat> LiveAsync(string code)
    {
        if (await sessions.FindAsync(code) is not { Phase: AudiencePhase.Live, SessionId: { } id } found)
        {
            throw new NotFoundException(GuestViews.NO_SESSION);
        }
        return new Seat(found.DeckId, Guid.Parse(id));
    }

    private async Task<Seat> SeatAsync(string code, string? guestId, CancellationToken cancellationToken)
    {
        var seated = await LiveAsync(code);
        var joined = guestId is not null && await db.AudienceJoins.AsNoTracking()
            .AnyAsync(j => j.SessionId == seated.SessionId && j.GuestId == guestId, cancellationToken);
        if (!joined)
        {
            throw new NotFoundException(GuestViews.NO_SESSION);
        }
        return seated;
    }

    private async Task<LoadedRound> RoundAsync(Seat seated, int slide, CancellationToken cancellationToken)
    {
        var row = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == seated.DeckId && s.DeletedAt == null && s.Position == slide - 1)
            .Select(s => new { s.Id, s.Layout, s.SlotsJson })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null || row.Layout != LAYOUT)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }
        var question = QuestionOf(row.SlotsJson);
        var responseId = await db.OpenResponses.AsNoTracking()
            .Where(r => r.SlideId == row.Id && r.Kind == OpenResponseKind.OpenText)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (responseId is null)
        {
            return new LoadedRound(null, OpenResponsePhase.Draft, 1, 0, question, []);
        }
        var round = await db.OpenRounds.AsNoTracking()
            .Where(r => r.OpenResponseId == responseId && r.SessionId == seated.SessionId)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return new LoadedRound(null, OpenResponsePhase.Draft, 1, 0, question, []);
        }
        var phase = PhaseOf(round);
        var answers = await AnswersAsync(round.Id, cancellationToken);
        return new LoadedRound(phase == OpenResponsePhase.Draft ? null : round.Id, phase, round.Number, round.Revision, question, answers);
    }

    // Arrival order: when each guest first sent. An update of the text does not change it, so the guest's number stays.
    private async Task<IReadOnlyList<LoadedAnswer>> AnswersAsync(Guid roundId, CancellationToken cancellationToken)
    {
        var entries = await OpenTextWall.InArrivalOrder(db.OpenEntries.AsNoTracking(), roundId)
            .Select(e => new { e.Id, e.GuestId, e.Text, Hidden = e.HiddenAt != null })
            .ToListAsync(cancellationToken);
        return [.. entries.Select(entry => new LoadedAnswer(entry.Id, entry.GuestId, entry.Text, entry.Hidden))];
    }

    private async Task StoreAsync(Guid roundId, string guestId, string text, int revision, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var round = await db.OpenRounds.SingleAsync(r => r.Id == roundId, cancellationToken);
        if (PhaseOf(round) != OpenResponsePhase.Open)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }
        if (round.Revision != revision)
        {
            throw new ConflictException(OpenRoundChecks.STALE);
        }
        var entry = await db.OpenEntries.SingleOrDefaultAsync(e => e.RoundId == roundId && e.GuestId == guestId && e.Position == 0, cancellationToken);
        if (entry is null)
        {
            var last = await db.OpenEntries.Where(e => e.RoundId == roundId).MaxAsync(e => (long?)e.Arrival, cancellationToken);
            db.OpenEntries.Add(new OpenEntry
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                GuestId = guestId,
                Position = 0,
                Text = text,
                TextKey = OpenTextRules.KeyOf(text),
                Arrival = (last ?? 0) + 1,
            });
        }
        else
        {
            entry.Text = text;
            entry.TextKey = OpenTextRules.KeyOf(text);
        }
        round.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static GuestResponseView ViewOf(LoadedRound round, string? guestId)
    {
        var mine = guestId is null ? "" : round.Answers.FirstOrDefault(answer => answer.GuestId == guestId)?.Text ?? "";
        return new GuestResponseView(
            OpenResponseKind.OpenText,
            round.Phase,
            round.Phase == OpenResponsePhase.Draft ? null : round.RoundId?.ToString(),
            round.Number,
            round.Revision,
            round.Question,
            true,
            round.Answers.Count,
            null,
            mine);
    }

    private static IReadOnlyList<WallAnswer> AnswersOf(LoadedRound round)
        => OpenTextWall.Number(round.Answers.Select(answer => (answer.Text, answer.Hidden)));

    private static OpenTextTally Tally(string deckId, int slide, LoadedRound round, OpenResponseTarget target, IReadOnlyList<WallAnswer> answers)
        => new(
            deckId,
            target,
            slide,
            round.Phase,
            round.Phase == OpenResponsePhase.Draft ? null : round.RoundId?.ToString(),
            round.Number,
            round.Revision,
            true,
            round.Answers.Count,
            answers);

    private static OpenResponsePhase PhaseOf(OpenRound round)
    {
        if (round.OpenedAt is null)
        {
            return OpenResponsePhase.Draft;
        }
        return round.ClosedAt is null ? OpenResponsePhase.Open : OpenResponsePhase.Closed;
    }

    private static string QuestionOf(string slotsJson)
    {
        using var document = JsonDocument.Parse(slotsJson);
        if (document.RootElement.TryGetProperty("question", out var question) && question.GetString() is { Length: > 0 } text)
        {
            return text;
        }
        throw new InvalidOperationException("An open-text slide is stored without its question.");
    }

    private sealed record Seat(string DeckId, Guid SessionId);

    private sealed record LoadedRound(Guid? RoundId, OpenResponsePhase Phase, int Number, int Revision, string Question, IReadOnlyList<LoadedAnswer> Answers);

    private sealed record LoadedAnswer(Guid Id, string GuestId, string Text, bool Hidden);
}
