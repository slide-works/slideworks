using System.Text.Json;
using System.Text.Json.Nodes;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Translations.Mcp;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Translations;

// translate_slides (SW-883 K4): stores one language's lines across slides, all or nothing. Every slide is checked before
// anything is written: the language is one of the deck's translations, each revision matches, each path is a line, and the
// merged slots pass the validator with the fit widths of the language's script. Then one transaction takes the deck's next
// revision, stores the lines, stamps the slides it changed and records the activity, and DeckChanged is published.
[RegisterPerRequest]
public class TranslationWrites(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IDeckTranslations translations,
    ITemplates templates,
    IActivityLog activity,
    IDeckEvents events,
    TimeProvider clock)
{
    public const string TOOL = "translate_slides";
    public const int MAX_SLIDES = 50;

    public async Task<TranslateSlidesResult> TranslateAsync(string deckId, TranslateSlidesChange change)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var entries = CheckEntries(change.Slides);

        TranslateSlidesResult result;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // EF opens the transaction with SQLite's BEGIN IMMEDIATE, so nothing changes the deck between the checks and the write.
            var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
            var language = CheckLanguage(await translations.ForDeckAsync(deckId), change.Language);
            var template = await templates.ForDeckAsync(deck, CancellationToken.None);
            var live = await db.Slides.Where(s => s.DeckId == deckId && s.DeletedAt == null).OrderBy(s => s.Position).ToListAsync();
            var widths = FitWidths.ForScript(template.FitWidths, language.Code);

            var planned = new List<PlannedSlide>();
            foreach (var entry in entries)
            {
                var plan = planned.Find(p => p.Number == entry.Slide) ?? Begin(live, template, language.Code, entry.Slide, planned);
                if (plan.Slide.Revision != entry.Revision)
                {
                    throw new ConflictException($"Slide {entry.Slide} changed since revision {entry.Revision}; read it again with get_deck");
                }
                plan.Apply(entry.Lines, language.Code);
                plan.Check(language, widths);
            }

            await db.Decks.Where(d => d.Id == deckId)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            var revision = await db.Decks.Where(d => d.Id == deckId).Select(d => d.Revision).SingleAsync();
            foreach (var plan in planned)
            {
                plan.Store(language.Code, revision);
            }
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, TOOL, Summary(entries, language)));
            await transaction.CommitAsync();

            result = new TranslateSlidesResult(language.Code, revision, [.. planned.Select(plan => plan.Result())], []);
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return result;
    }

    private static IReadOnlyList<SlideLinesEntry> CheckEntries(IReadOnlyList<SlideLinesEntry?>? slides)
    {
        if (slides is null || slides.Count == 0)
        {
            throw new BadRequestException("slides: at least 1");
        }
        if (slides.Count > MAX_SLIDES)
        {
            throw new BadRequestException($"give at most {MAX_SLIDES} slides");
        }
        if (slides.Any(slide => slide?.Lines is null))
        {
            throw new BadRequestException("slides: an entry is missing");
        }
        return [.. slides.Select(slide => slide!)];
    }

    // The language must be one of the deck's translations while they are on; the primary never is (K2).
    private static Language CheckLanguage(TranslationSettings settings, string code)
    {
        if (string.Equals(code, settings.Primary, StringComparison.Ordinal))
        {
            throw new BadRequestException($"{code} is the primary language; change the slides with update_slide instead");
        }
        if (!settings.Enabled || !settings.Languages.Contains(code, StringComparer.Ordinal) || LanguageCatalogue.Find(code) is not { } language)
        {
            throw new BadRequestException($"turn translations on with set_translations and add {code} first");
        }
        return language;
    }

    private static PlannedSlide Begin(List<Slide> live, ResolvedTemplate template, string language, int number, List<PlannedSlide> planned)
    {
        if (number < 1 || number > live.Count)
        {
            throw new BadRequestException(live.Count == 0 ? "slide: the deck has no slides" : $"slide: must be between 1 and {live.Count}");
        }
        var plan = new PlannedSlide(number, live[number - 1], template.FindLayout(live[number - 1].Layout), language);
        planned.Add(plan);
        return plan;
    }

    // "translated 4 slides into Thai", or "removed 2 Thai lines on slide 3" when the call only removes lines.
    private static string Summary(IReadOnlyList<SlideLinesEntry> entries, Language language)
    {
        var slides = entries.Select(entry => entry.Slide).Distinct().ToList();
        var values = entries.SelectMany(entry => entry.Lines.Select(line => line.Value)).ToList();
        if (values.Count == 0 || values.Any(value => value is not null))
        {
            return $"translated {Count(slides.Count, "slide")} into {language.EnglishName}";
        }
        var where = slides.Count == 1 ? $"slide {slides[0]}" : $"{slides.Count} slides";
        return $"removed {Count(values.Count, $"{language.EnglishName} line")} on {where}";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    // One slide of the call: its stored lines in the language as the call leaves them, and the merge they make.
    private sealed class PlannedSlide(int number, Slide slide, LayoutDefinition? layout, string language)
    {
        private readonly JsonElement _slots = SlideJson.Slots(slide);
        private readonly LayoutDefinition? _layout = layout;
        private readonly IReadOnlyList<SlotLine> _lines = layout is null ? [] : TranslatableLines.Of(layout, SlideJson.Slots(slide));
        private readonly Dictionary<string, TranslationEntry> _entries = SlideTranslationsJson.Read(slide.TranslationsJson).TryGetValue(language, out var stored)
            ? new Dictionary<string, TranslationEntry>(stored, StringComparer.Ordinal)
            : new Dictionary<string, TranslationEntry>(StringComparer.Ordinal);
        private MergedSlide _merged = new(SlideJson.Slots(slide), 0, []);

        public int Number { get; } = number;

        public Slide Slide { get; } = slide;

        // A text sets the line, translated from the line's current primary text; null removes it.
        public void Apply(JsonObject lines, string code)
        {
            foreach (var (path, value) in lines)
            {
                var line = _lines.FirstOrDefault(l => string.Equals(l.Path, path, StringComparison.Ordinal))
                    ?? throw new BadRequestException($"slide {Number}: {path} is not a line on this slide; read the lines with get_deck and language {code}");
                if (value is null)
                {
                    _entries.Remove(path);
                }
                else if (value is JsonValue text && text.GetValueKind() == JsonValueKind.String)
                {
                    _entries[path] = new TranslationEntry(text.GetValue<string>(), line.Text);
                }
                else
                {
                    throw new BadRequestException($"slide {Number}: {path} must be text, or null to remove the line");
                }
            }
        }

        // The merged slots must pass the validator in the language's script. An emphasis whose phrase is not in its translated
        // text is refused here rather than dropped.
        public void Check(Language language, FitWidths widths)
        {
            if (_layout is null)
            {
                return;
            }
            _merged = SlideTranslation.Merge(_layout, _slots, _entries, dropUnmatchedEmphasis: false);
            var validation = SlideValidator.Validate(_layout, _merged.Slots, widths, Slide.Surface);
            if (!validation.IsValid)
            {
                throw new BadRequestException($"slide {Number} in {language.EnglishName}: {validation.Error}");
            }
        }

        // Stores the lines and stamps the slide with the deck's new revision, when they changed.
        public void Store(string code, long revision)
        {
            var all = SlideTranslationsJson.Read(Slide.TranslationsJson).ToDictionary(StringComparer.Ordinal);
            all[code] = _entries;
            var json = SlideTranslationsJson.Write(all);
            if (string.Equals(json, Slide.TranslationsJson, StringComparison.Ordinal))
            {
                return;
            }
            Slide.TranslationsJson = json;
            Slide.Revision = revision;
        }

        public TranslatedSlide Result()
        {
            var lines = new JsonObject();
            foreach (var path in _lines.Select(line => line.Path).Where(_entries.ContainsKey))
            {
                lines[path] = _entries[path].Text;
            }
            var coverage = TranslationCoverage.Of([_merged]);
            return new TranslatedSlide(Number, Slide.Revision, lines, coverage.Current, coverage.Stale, coverage.Missing);
        }
    }
}
