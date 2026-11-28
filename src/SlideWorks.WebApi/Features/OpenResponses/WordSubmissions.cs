using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Join;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A guest's view of a word-cloud slide and their words submit (word-cloud.json, D2, D13, D14). A code with no live
// session is 404 no_session. A submit is refused before any write, in order: no session, guest off the roster, the
// rate limit, a stale or non-open round, then OpenTextRules (which refuses a profane word last).
[RegisterPerRequest]
public class WordSubmissions(AppDbContext db, IAudienceSessions sessions, OpenResponseLimiter limiter, IWordCloudPublisher publisher)
{
    public const string NO_GUEST = "no_guest";

    private const string WORD_CLOUD = "word-cloud";

    // TODO: the language is ignored and the prompt is the primary one; translating the guest's response view replaces this.
    public async Task<GuestResponseView> ViewAsync(string code, int slide, string? guestId, string? language, CancellationToken cancellationToken)
    {
        var live = await LiveAsync(code);
        return await ReadAsync(live, slide, guestId, cancellationToken);
    }

    public async Task<StoredWords> SubmitAsync(string code, int slide, string guestId, WordsRequest request, CancellationToken cancellationToken)
    {
        var live = await LiveAsync(code);
        if (!await db.AudienceJoins.AsNoTracking().AnyAsync(j => j.SessionId == live.SessionId && j.GuestId == guestId, cancellationToken))
        {
            throw new NotFoundException(NO_GUEST);
        }

        limiter.Take(guestId, code, slide);

        var view = await ReadAsync(live, slide, guestId, cancellationToken);
        OpenRoundChecks.RequireCurrent(view, request.Round, request.Revision);
        OpenRoundChecks.RequireValid(OpenTextRules.Check(request.Words));

        if (view.Round is not { } roundKey)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }

        var held = await ReplaceAsync(live.SessionId, Guid.Parse(roundKey), guestId, OpenTextRules.WordsOf(request.Words), request.Revision, cancellationToken);
        await publisher.PublishAsync(live.DeckId, slide, cancellationToken);
        return new StoredWords(held);
    }

    private async Task<Live> LiveAsync(string code)
    {
        if (await sessions.FindAsync(code) is not { Phase: AudiencePhase.Live, SessionId: { } id } found)
        {
            throw new NotFoundException(GuestViews.NO_SESSION);
        }

        return new Live(found.DeckId, Guid.Parse(id));
    }

    private async Task<GuestResponseView> ReadAsync(Live live, int slide, string? guestId, CancellationToken cancellationToken)
    {
        var row = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == live.DeckId && s.DeletedAt == null && s.Position == slide - 1)
            .Select(s => new { s.Id, s.Layout, s.SlotsJson })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(GuestViews.NO_SESSION);
        if (row.Layout != WORD_CLOUD)
        {
            throw new NotFoundException(GuestViews.NO_SESSION);
        }

        var question = QuestionOf(row.SlotsJson);
        var responseId = await db.OpenResponses.AsNoTracking()
            .Where(r => r.SlideId == row.Id && r.Kind == OpenResponseKind.WordCloud)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var round = responseId == null
            ? null
            : await db.OpenRounds.AsNoTracking()
                .Where(r => r.OpenResponseId == responseId && r.SessionId == live.SessionId)
                .OrderByDescending(r => r.Number)
                .FirstOrDefaultAsync(cancellationToken);
        if (round?.OpenedAt == null)
        {
            return new GuestResponseView(
                OpenResponseKind.WordCloud,
                OpenResponsePhase.Draft,
                null,
                round?.Number ?? 1,
                round?.Revision ?? 0,
                question,
                true,
                0,
                [],
                null);
        }

        var entries = await db.OpenEntries.AsNoTracking()
            .Where(e => e.RoundId == round.Id)
            .Select(e => new { e.GuestId, e.Position, e.Text })
            .ToListAsync(cancellationToken);
        var mine = guestId == null
            ? []
            : entries.Where(e => e.GuestId == guestId).OrderBy(e => e.Position).Select(e => e.Text).ToArray();
        return new GuestResponseView(
            OpenResponseKind.WordCloud,
            round.ClosedAt == null ? OpenResponsePhase.Open : OpenResponsePhase.Closed,
            round.Id.ToString(),
            round.Number,
            round.Revision,
            question,
            true,
            entries.Select(e => e.GuestId).Distinct().Count(),
            mine,
            null);
    }

    // Deletes this guest's rows and inserts the new set, in one transaction. A word one typo from a word other guests sent
    // is stored under that word's key (SW-606), and a word keeps the casing of the earliest row (by arrival) already stored for its key, so the
    // group shows one spelling. The round is checked again here so a round that closed or moved on after the read writes nothing.
    private async Task<IReadOnlyList<string>> ReplaceAsync(
        Guid sessionId,
        Guid roundId,
        string guestId,
        IReadOnlyList<string> words,
        int revision,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var round = await db.OpenRounds.SingleAsync(r => r.Id == roundId, cancellationToken);
        if (round.SessionId != sessionId || round.OpenedAt == null || round.ClosedAt != null)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }

        if (round.Revision != revision)
        {
            throw new ConflictException(OpenRoundChecks.STALE);
        }

        var saved = await db.OpenEntries.AsNoTracking()
            .Where(e => e.RoundId == roundId)
            .OrderBy(e => e.Arrival)
            .ThenBy(e => e.Position)
            .Select(e => new { e.GuestId, e.TextKey, e.Text, e.Arrival })
            .ToListAsync(cancellationToken);
        var first = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in saved)
        {
            first.TryAdd(entry.TextKey, entry.Text);
        }

        // A near miss joins the word most other guests hold; this guest's old words are being replaced, so they don't count.
        var known = saved
            .Where(e => e.GuestId != guestId)
            .GroupBy(e => e.TextKey, StringComparer.Ordinal)
            .OrderByDescending(g => g.Select(e => e.GuestId).Distinct(StringComparer.Ordinal).Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .ToList();
        // The guest keeps the place of their first send; a guest with no rows yet comes after every row in the round.
        var arrival = saved.Where(e => e.GuestId == guestId).Select(e => (long?)e.Arrival).Min() ?? (saved.Count == 0 ? 0 : saved.Max(e => e.Arrival)) + 1;
        await db.OpenEntries.Where(e => e.RoundId == roundId && e.GuestId == guestId).ExecuteDeleteAsync(cancellationToken);
        var held = new List<string>(words.Count);
        var mine = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var key = OpenTextRules.GroupKeyOf(OpenTextRules.KeyOf(word), known);
            if (!mine.Add(key))
            {
                continue;
            }

            known.Add(key);
            var text = first.TryGetValue(key, out var casing) ? casing : word;
            first.TryAdd(key, text);
            db.OpenEntries.Add(new OpenEntry
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                GuestId = guestId,
                Position = held.Count,
                Text = text,
                TextKey = key,
                Arrival = arrival,
            });
            held.Add(text);
        }

        round.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return held;
    }

    private static string QuestionOf(string slotsJson)
    {
        using var document = JsonDocument.Parse(slotsJson);
        if (!document.RootElement.TryGetProperty("question", out var question) || question.GetString() is not { } text)
        {
            throw new InvalidOperationException("A word-cloud slide has no question slot.");
        }

        return text;
    }

    private readonly record struct Live(string DeckId, Guid SessionId);
}
