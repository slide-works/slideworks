using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.OpenResponses;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The presenter's control view of a word-cloud or open-text slide and its open, close and new-round actions
// (open-response-control.json), the agent's open and close (D7), opening on arrival and closing on leaving (D5), and
// closing every open round when the live session ends (D12). A deck the caller can't see is 404 and one they can't present
// is 403. Every check runs before anything is written, so a refused action changes nothing. Results stay live (D14): a round
// shows its entries from the moment it opens, and there is no reveal. Each change bumps the round's revision and publishes
// the slide's event.
[RegisterPerRequest]
public class OpenResponseControls(
    AppDbContext db,
    IDeckAccess access,
    ICurrentActor actor,
    IAudienceSessions sessions,
    IWordCloudPublisher wordClouds,
    IOpenTextPublisher openTexts,
    TimeProvider clock)
{
    public const string NOT_LIVE = "not_live";
    public const string NOT_AN_OPEN_RESPONSE = "Open-response slide not found";

    public async Task<OpenResponseControlView> ViewAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        return ViewOf(await LocateAsync(deckId, slide));
    }

    public async Task<OpenResponseControlView> OpenAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        return await ChangeAsync(deckId, slide, agentKind: null, WithOpenRound);
    }

    public async Task<OpenResponseControlView> CloseAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        return await ChangeAsync(deckId, slide, agentKind: null, WithClosedRound);
    }

    public async Task<OpenResponseControlView> NewRoundAsync(string deckId, int slide)
    {
        await RequirePresenterAsync(deckId);
        return await ChangeAsync(deckId, slide, agentKind: null, WithNextRound);
    }

    // An agent needs edit access, not the right to present, and a slide of the tool's kind (D7).
    public async Task<OpenResponseControlView> OpenAsAgentAsync(string deckId, int slide, OpenResponseKind kind)
    {
        await RequireEditorAsync(deckId);
        return await ChangeAsync(deckId, slide, kind, WithOpenRound);
    }

    public async Task<OpenResponseControlView> CloseAsAgentAsync(string deckId, int slide, OpenResponseKind kind)
    {
        await RequireEditorAsync(deckId);
        return await ChangeAsync(deckId, slide, kind, WithClosedRound);
    }

    // The presenter is on the slide: every other slide's open round in the live session closes, and this slide's round opens
    // when the session has none for it yet, so coming back to a slide does not reopen it.
    public async Task ArriveAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        List<(OpenRound Round, OpenResponseKind Kind, int? Slide)> leaving;
        Located? opened;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (await LiveSessionAsync(deckId) is not { } session)
            {
                return;
            }
            var row = await SlideAsync(deckId, slide);
            leaving = await OpenRoundsAsync(deckId, session, row?.Id, cancellationToken);
            leaving.ForEach(open => MarkClosed(open.Round));
            var arriving = row == null || KindOf(row) == null ? null : await LocateAsync(row, session);
            opened = arriving is { Current: null } ? arriving : null;
            if (opened != null)
            {
                Insert(opened, session, 1);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await PublishAsync(deckId, leaving, cancellationToken);
        if (opened != null)
        {
            await PublishAsync(deckId, slide, opened.Kind, cancellationToken);
        }
    }

    // The live session is ending: every open round in it closes, keeping its entries.
    public async Task EndSessionAsync(string deckId, Guid sessionId, CancellationToken cancellationToken)
    {
        List<(OpenRound Round, OpenResponseKind Kind, int? Slide)> open;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            open = await OpenRoundsAsync(deckId, sessionId, exceptSlide: null, cancellationToken);
            open.ForEach(found => MarkClosed(found.Round));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await PublishAsync(deckId, open, cancellationToken);
    }

    // The slide's latest round in the live session and its rows. A slide that is not word-cloud or open-text is 404.
    public async Task<Located> LocateAsync(string deckId, int slide)
    {
        var row = await SlideAsync(deckId, slide);
        if (row == null || KindOf(row) == null)
        {
            throw new NotFoundException(NOT_AN_OPEN_RESPONSE);
        }
        return await LocateAsync(row, await LiveSessionAsync(deckId));
    }

    // Publishes the slide's event and reads its control view back.
    public async Task<OpenResponseControlView> AnnounceAsync(Located located)
    {
        await PublishAsync(located.Slide.DeckId, located.Slide.Position + 1, located.Kind, CancellationToken.None);
        return ViewOf(located);
    }

    // The control entries of a round and the rows behind each (D11): a word is every row with its text key, numbered by
    // count descending then text key, shown in the casing of its earliest row; an answer is its guest's row, numbered by
    // when the guest first sent (Arrival), so a number never changes when later answers arrive. An entry is hidden only when every
    // row behind it is, as the audience sees it, so a row sent after a hide shows the entry again.
    public static IReadOnlyList<(ControlEntry Entry, IReadOnlyList<OpenEntry> Rows)> EntriesOf(OpenResponseKind kind, IReadOnlyList<OpenEntry> rows)
        => kind == OpenResponseKind.WordCloud
            ? [.. rows.GroupBy(row => row.TextKey, StringComparer.Ordinal)
                .Select(word => (Key: word.Key, Rows: (IReadOnlyList<OpenEntry>)[.. word], Count: word.Select(row => row.GuestId).Distinct(StringComparer.Ordinal).Count()))
                .OrderByDescending(word => word.Count)
                .ThenBy(word => word.Key, StringComparer.Ordinal)
                .Select((word, index) => (new ControlEntry(index + 1, word.Rows.OrderBy(row => row.Arrival).ThenBy(row => row.Position).ThenBy(row => row.Text, StringComparer.Ordinal).First().Text, word.Count, word.Rows.All(row => row.HiddenAt != null)), word.Rows))]
            : [.. rows.GroupBy(row => row.GuestId, StringComparer.Ordinal)
                .OrderBy(guest => guest.Min(row => row.Arrival))
                .ThenBy(guest => guest.Key, StringComparer.Ordinal)
                .Select((guest, index) => (Answer: guest.MinBy(row => row.Position)!, Rows: (IReadOnlyList<OpenEntry>)[.. guest], Number: index + 1))
                .Select(guest => (new ControlEntry(guest.Number, guest.Answer.Text, null, guest.Rows.All(row => row.HiddenAt != null)), guest.Rows))];

    // One write transaction (EF begins SQLite's BEGIN IMMEDIATE) around the read, the checks and the write, so two calls on
    // the same slide can't both pass a check. The change returns the slide as it now stands, or null when nothing changed;
    // the event goes out only after the commit.
    private async Task<OpenResponseControlView> ChangeAsync(string deckId, int slide, OpenResponseKind? agentKind, Func<Located, Located?> change)
    {
        Located located;
        Located? changed;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            located = await LocateAsync(deckId, slide);
            if (agentKind is { } kind)
            {
                RequireAgentSlide(located, kind);
            }
            changed = change(located);
            if (changed != null)
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
        return changed == null ? ViewOf(located) : await AnnounceAsync(changed);
    }

    // Opens round 1 when the session has no round for the slide; an open round is returned unchanged.
    private Located? WithOpenRound(Located located)
    {
        var session = located.Session ?? throw new ConflictException(NOT_LIVE);
        if (located.Current == null)
        {
            return located with { Current = Insert(located, session, 1) };
        }
        return PhaseOf(located.Current) == OpenResponsePhase.Open ? null : throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
    }

    private Located WithClosedRound(Located located)
    {
        if (located.Session == null)
        {
            throw new ConflictException(NOT_LIVE);
        }
        if (located.Current == null || PhaseOf(located.Current) != OpenResponsePhase.Open)
        {
            throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        }
        MarkClosed(located.Current);
        return located;
    }

    // Closes the slide's round when it is open, then opens the next number with no entries.
    private Located WithNextRound(Located located)
    {
        var session = located.Session ?? throw new ConflictException(NOT_LIVE);
        var current = located.Current ?? throw new ConflictException(OpenRoundChecks.WRONG_PHASE);
        if (PhaseOf(current) == OpenResponsePhase.Open)
        {
            MarkClosed(current);
        }
        return located with { Current = Insert(located, session, current.Number + 1), Rows = [] };
    }

    private async Task RequireEditorAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }

    private static void RequireAgentSlide(Located located, OpenResponseKind kind)
    {
        if (located.Kind != kind)
        {
            throw new NotFoundException(NOT_AN_OPEN_RESPONSE);
        }
        if (located.Session == null)
        {
            throw new ConflictException($"{NOT_LIVE}: the deck is not live; a person starts the audience session with Go live");
        }
    }

    private async Task<Located> LocateAsync(Slide row, Guid? session)
    {
        var response = await db.OpenResponses.SingleOrDefaultAsync(r => r.SlideId == row.Id);
        var current = response == null || session == null
            ? null
            : await db.OpenRounds.Where(r => r.OpenResponseId == response.Id && r.SessionId == session).OrderByDescending(r => r.Number).FirstOrDefaultAsync();
        var rows = current == null ? [] : await db.OpenEntries.Where(e => e.RoundId == current.Id).ToListAsync();
        return new Located(row, KindOf(row)!.Value, response, session, current, rows);
    }

    private Task<Slide?> SlideAsync(string deckId, int slide)
        => db.Slides.AsNoTracking().SingleOrDefaultAsync(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1);

    private async Task<Guid?> LiveSessionAsync(string deckId)
        => await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live, SessionId: { } id } ? Guid.Parse(id) : null;

    // The open rounds of the deck's open-response slides in the session, but not the excepted slide's, with the slide number
    // to publish (null for a slide since deleted, which has no number).
    private async Task<List<(OpenRound Round, OpenResponseKind Kind, int? Slide)>> OpenRoundsAsync(string deckId, Guid session, Guid? exceptSlide, CancellationToken cancellationToken)
    {
        var open = await (
            from round in db.OpenRounds
            join response in db.OpenResponses on round.OpenResponseId equals response.Id
            join slide in db.Slides on response.SlideId equals slide.Id
            where response.DeckId == deckId && round.SessionId == session && round.OpenedAt != null && round.ClosedAt == null && (Guid?)response.SlideId != exceptSlide
            select new { round, response.Kind, slide.Position, slide.DeletedAt }).ToListAsync(cancellationToken);
        return [.. open.Select(found => (found.round, found.Kind, found.DeletedAt == null ? found.Position + 1 : (int?)null))];
    }

    private OpenRound Insert(Located located, Guid session, int number)
    {
        var response = located.Response;
        if (response == null)
        {
            response = new OpenResponse { Id = Guid.NewGuid(), DeckId = located.Slide.DeckId, SlideId = located.Slide.Id, Kind = located.Kind };
            db.OpenResponses.Add(response);
        }
        var round = new OpenRound { Id = Guid.NewGuid(), OpenResponseId = response.Id, SessionId = session, Number = number, OpenedAt = clock.GetUtcNow() };
        db.OpenRounds.Add(round);
        return round;
    }

    private void MarkClosed(OpenRound round)
    {
        round.ClosedAt = clock.GetUtcNow();
        round.Revision++;
    }

    private async Task PublishAsync(string deckId, List<(OpenRound Round, OpenResponseKind Kind, int? Slide)> rounds, CancellationToken cancellationToken)
    {
        foreach (var (_, kind, slide) in rounds)
        {
            if (slide is { } number)
            {
                await PublishAsync(deckId, number, kind, cancellationToken);
            }
        }
    }

    private Task PublishAsync(string deckId, int slide, OpenResponseKind kind, CancellationToken cancellationToken) => kind == OpenResponseKind.WordCloud
        ? wordClouds.PublishAsync(deckId, slide, cancellationToken)
        : openTexts.PublishAsync(deckId, slide, cancellationToken);

    public static OpenResponseControlView ViewOf(Located located)
    {
        var current = located.Current;
        var phase = current == null ? OpenResponsePhase.Draft : PhaseOf(current);
        return new OpenResponseControlView(
            located.Kind,
            phase,
            phase == OpenResponsePhase.Draft ? null : current!.Id.ToString(),
            current?.Number ?? 1,
            current?.Revision ?? 0,
            OpenResponseResults.Live,
            OpenResponseOpens.Arrival,
            true,
            located.Rows.Select(row => row.GuestId).Distinct(StringComparer.Ordinal).Count(),
            [.. EntriesOf(located.Kind, located.Rows).Select(found => found.Entry)]);
    }

    private static OpenResponsePhase PhaseOf(OpenRound round) => round switch
    {
        { ClosedAt: not null } => OpenResponsePhase.Closed,
        { OpenedAt: not null } => OpenResponsePhase.Open,
        _ => OpenResponsePhase.Draft,
    };

    private static OpenResponseKind? KindOf(Slide slide)
    {
        if (slide.Layout == WordCloudLayout.Definition.Name)
        {
            return OpenResponseKind.WordCloud;
        }
        return slide.Layout == OpenTextLayout.Definition.Name ? OpenResponseKind.OpenText : null;
    }

    public async Task RequirePresenterAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (!await access.CanPresentAsync(deckId, actor))
        {
            throw new ForbiddenException("Only someone who can present the deck can run its open responses");
        }
    }

    // A slide's open-response record and its latest round in the live session, with that round's rows. Response is null
    // until the slide's first round opens; Session is null when the deck is not live; Current is null when the session has
    // no round for the slide yet.
    public sealed record Located(Slide Slide, OpenResponseKind Kind, OpenResponse? Response, Guid? Session, OpenRound? Current, IReadOnlyList<OpenEntry> Rows);
}
