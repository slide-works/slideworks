using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Qa;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks.Mcp;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Translations;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Decks;

// The two reads every screen and tool builds on (K5): the play view for viewers and get_deck for the owner's agent.
// Deleted slides are never returned.
[RegisterPerRequest]
public class DeckReader(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IPublicUrls urls,
    JoinViews joinViews,
    IDeckLooks looks,
    ITemplates templates,
    QaRows qa,
    IDeckTranslations translations,
    TranslationsSummary translationsSummary)
{
    public const string NOT_FOUND = "Deck not found";

    public async Task<DeckPlayView> GetPlayAsync(string deckId)
    {
        if (!await access.CanViewAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        return await PlayAsync(await LoadDeckAsync(deckId), await access.CanPresentAsync(deckId, actor));
    }

    // For the render API (behind a render token) and the card cache only (L9): the play view with the deck's revision
    // and sharing, and no access check. An unknown deck is 404 "Deck not found".
    public async Task<DeckForRender> ReadForRenderAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deckId) ?? throw new NotFoundException(NOT_FOUND);
        return new DeckForRender(await PlayAsync(deck, isOwner: false), deck.Subtitle, deck.Revision, deck.Sharing);
    }

    // The owner or a person with write (C5), or an agent acting for either: the slides carry notes and revisions. While
    // translations are on, the deck carries its translations summary (SW-883 K4). With a language of the deck's list, each slide
    // also lists its lines with their state in it; the primary language reads as no language. Any other language is refused.
    public async Task<object> GetForEditorAsync(string deckId, bool summary = false, string? language = null)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(NOT_FOUND);
        }
        var deck = await LoadDeckAsync(deckId);
        var translationSettings = await translations.ForDeckAsync(deckId);
        var translated = TranslatedLanguage(translationSettings, language);
        var slides = await LiveSlidesAsync(deckId);
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        var url = urls.Deck(deck.Id, deck.Slug);
        var settings = await qa.SettingsAsync(deckId);
        var reactions = await ReactionsAsync(slides);
        var translationsView = translationSettings.Enabled ? await translationsSummary.ForDeckAsync(deckId) : null;
        IReadOnlyList<LineView>? LinesOf(Slide slide) =>
            translated is null ? null : TranslationMerge.For(slide, template, translationSettings, translated).Lines;
        return summary
            ? GetDeckSummary.From(deck, slides, template, url, settings, reactions, translationsView, LinesOf)
            : GetDeckResult.From(deck, slides, template, url, settings, reactions, translationsView, LinesOf);
    }

    // The language whose lines get_deck lists: null for no language or the primary, else one of the deck's languages while
    // translations are on. Anything else is refused, naming the languages the deck has.
    private static string? TranslatedLanguage(TranslationSettings settings, string? language)
    {
        if (language is null || string.Equals(language, settings.Primary, StringComparison.Ordinal))
        {
            return null;
        }

        var offered = settings.Enabled ? settings.Languages : [];
        if (!offered.Contains(language, StringComparer.Ordinal))
        {
            throw new BadRequestException($"{language} is not one of this deck's languages ({string.Join(", ", offered.Prepend(settings.Primary))})");
        }

        return language;
    }

    // Each session's reaction totals per slide of this deck, newest session first. The totals are rows of the Q&A feature,
    // read here as counts only.
    private async Task<IReadOnlyList<GetDeckReactionSession>> ReactionsAsync(IReadOnlyList<Slide> slides)
    {
        var numbers = slides.ToDictionary(s => s.Id, s => s.Position + 1);
        var ids = numbers.Keys.ToList();
        var totals = await db.ReactionTotals.AsNoTracking().Where(t => ids.Contains(t.SlideId)).ToListAsync();
        var sessionIds = totals.Select(t => t.SessionId).Distinct().ToList();
        var starts = await db.AudienceSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).Select(s => new { s.Id, s.StartedAt }).ToListAsync();
        return [.. starts.OrderByDescending(s => s.StartedAt).Select(session => new GetDeckReactionSession(
            session.StartedAt,
            [.. totals.Where(t => t.SessionId == session.Id).GroupBy(t => numbers[t.SlideId]).OrderBy(g => g.Key).Select(g => new GetDeckReactionSlide(g.Key, CountsOf(g)))]))];
    }

    private static ReactionCounts CountsOf(IEnumerable<ReactionTotal> totals)
    {
        var count = (ReactionEmoji emoji) => totals.Where(t => t.Emoji == emoji).Sum(t => t.Count);
        return new ReactionCounts(count(ReactionEmoji.ThumbsUp), count(ReactionEmoji.Heart), count(ReactionEmoji.Laugh), count(ReactionEmoji.Wow), count(ReactionEmoji.Clap));
    }

    private async Task<DeckPlayView> PlayAsync(Deck deck, bool isOwner)
    {
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        return DeckPlayView.From(deck, await LiveSlidesAsync(deck.Id), template, looks.Of(deck, template), await joinViews.ForAsync(deck), isOwner);
    }

    private Task<Deck> LoadDeckAsync(string deckId) => db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);

    private Task<List<Slide>> LiveSlidesAsync(string deckId)
        => db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .ToListAsync();
}
