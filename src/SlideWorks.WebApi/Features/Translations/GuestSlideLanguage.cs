using System.Text.Json.Nodes;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Translations;

// One question slide in a guest's language (SW-883 K3, D17), for the audience's reads of a poll, quiz, word-cloud or open-text
// slide. The merge is TranslationMerge.For. It takes no ICurrentActor. Per request, because it uses the database.
[RegisterPerRequest]
public class GuestSlideLanguage(AppDbContext db, IDeckTranslations translations, ITemplates templates)
{
    // The slide with the language applied, or its own slots when the deck does not offer the language (null included).
    public async Task<MergedSlide> MergeAsync(Slide slide, string? language)
        => await MergeAsync(slide, language, await translations.ForDeckAsync(slide.DeckId));

    // The same with the deck's settings already read. Settings come first so a language that is not effective costs no deck or
    // template load.
    public async Task<MergedSlide> MergeAsync(Slide slide, string? language, TranslationSettings settings)
    {
        if (!settings.Enabled || language is null || !settings.Languages.Contains(language, StringComparer.Ordinal))
        {
            return new MergedSlide(SlideJson.Slots(slide), 0, []);
        }

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == slide.DeckId);
        return TranslationMerge.For(slide, await templates.ForDeckAsync(deck, CancellationToken.None), settings, language);
    }

    // A closed round's option texts as the round ran with them (D17). The slide's slots are built with the options replaced by
    // the snapshot texts and merged by TranslationMerge.For, which marks a line Current only when its entry's From equals the
    // line text; so a snapshot option is translated only when it still matches, and otherwise shows the snapshot text.
    public async Task<IReadOnlyList<string>> SnapshotOptionsAsync(Slide slide, string? language, IReadOnlyList<string> snapshot, TranslationSettings settings)
    {
        var slots = JsonNode.Parse(slide.SlotsJson)!.AsObject();
        slots["options"] = new JsonArray([.. snapshot.Select(text => (JsonNode?)JsonValue.Create(text))]);
        var merged = await MergeAsync(new Slide { DeckId = slide.DeckId, Layout = slide.Layout, SlotsJson = slots.ToJsonString(), TranslationsJson = slide.TranslationsJson }, language, settings);
        return PollReader.SlotsOf(merged.Slots.GetRawText()).Options;
    }
}
