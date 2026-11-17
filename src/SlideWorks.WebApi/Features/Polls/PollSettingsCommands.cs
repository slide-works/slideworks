using System.Text.Json;
using System.Text.Json.Nodes;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// What an agent changes on a poll slide without a round (update_poll, add_poll_option). Both run through SlideEdits.EditAsync,
// so each takes the deck's next revision, records one activity entry under its tool's name and publishes DeckChanged, and both
// need edit access (404 "Deck not found" otherwise). Every check comes before any write, so a refused call changes nothing.
// - Settings: validated by PollSettingsRules, the one place, against the slide's option count. Refused with a conflict while a
//   round is open on the slide. A draft round or a closed one doesn't block it: a draft has no answers to break and no phone
//   has answered it, and a closed round keeps its Final. The phones and the presenter read the settings, so a change on a live
//   deck is announced.
// - Add option: appended at the end, so no position moves, and allowed while a round is open (D13). The slide's slots are
//   checked by the layout's own validator (at most 8 options, each up to 60 characters). Announced on a live deck.
// Per request.
[RegisterPerRequest]
public class PollSettingsCommands(SlideEdits edits, AppDbContext db, PollReader reader, PollEdits pollEdits, IDeckImages images)
{
    public const string UPDATE_TOOL = "update_poll";
    public const string ADD_OPTION_TOOL = "add_poll_option";

    public async Task<PollSettings> UpdateAsync(string deckId, int slide, PollSettingsChange change)
    {
        var settings = await edits.EditAsync(deckId, UPDATE_TOOL, async deck =>
        {
            var (poll, current) = await PollAsync(deck.Id, slide);
            if (await pollEdits.IsRoundOpenAsync(deck.Id, poll.Id))
            {
                throw new ConflictException($"{PollEdits.POLL_OPEN}: a round of the poll on slide {slide} is open, so its settings can't change; close it first (close_poll)");
            }
            var next = Merge(current, change, PollReader.SlotsOf(poll.SlotsJson).Options.Count);
            var row = await db.Polls.SingleAsync(p => p.SlideId == poll.Id);
            row.Pick = next.Pick;
            row.Max = next.Max;
            row.Results = next.Results;
            row.Correct = next.Correct;
            row.Opens = next.Opens;
            await db.SaveChangesAsync();
            return (next, $"changed the settings of the poll on slide {slide}");
        });
        await pollEdits.AnnounceAsync(deckId, slide);
        return settings;
    }

    public async Task<PollOptionsResult> AddOptionAsync(string deckId, int slide, string text)
    {
        var options = await edits.EditAsync(deckId, ADD_OPTION_TOOL, async deck =>
        {
            var (poll, _) = await PollAsync(deck.Id, slide);
            var slots = JsonNode.Parse(poll.SlotsJson)!.AsObject();
            var list = slots["options"]!.AsArray();
            list.Add(text);
            var element = JsonSerializer.SerializeToElement(slots);
            await SlideChecks.ValidateAsync(images, deck.Id, PollRows.LAYOUT, element);

            var tracked = await db.Slides.SingleAsync(s => s.Id == poll.Id);
            tracked.SlotsJson = SlideJson.ForStorage(element);
            tracked.Revision = deck.Revision;
            await db.SaveChangesAsync();
            return (PollReader.SlotsOf(tracked.SlotsJson).Options, $"added option {list.Count} to the poll on slide {slide}");
        });
        await pollEdits.AnnounceAsync(deckId, slide);
        return new PollOptionsResult(options);
    }

    // The poll slide at that 1-based number with its settings: a number outside the deck, or a slide that isn't a poll, is
    // refused naming the argument.
    private async Task<(Slide Slide, PollSettings Settings)> PollAsync(string deckId, int slide)
    {
        var count = await db.Slides.CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        if (slide < 1 || slide > count)
        {
            throw new BadRequestException(count == 0 ? "slide: the deck has no slides" : $"slide: must be between 1 and {count}");
        }
        return await reader.SlideAsync(deckId, slide) ?? throw new BadRequestException($"slide: slide {slide} is not a poll; add one with add_slide and the poll layout");
    }

    // The settings with what was given changed. Switching pick to one clears max unless a max is given with it, which the
    // rules then refuse; correct 0 clears the correct answer. The merged settings are checked by PollSettingsRules.
    private static PollSettings Merge(PollSettings current, PollSettingsChange change, int optionCount)
    {
        var pick = change.Pick == null ? current.Pick : ParsePick(change.Pick);
        var max = change.Max ?? (change.Pick != null && pick == PollPick.One ? null : current.Max);
        var results = change.Results == null ? current.Results : ParseResults(change.Results);
        var correct = change.Correct switch
        {
            null => current.Correct,
            0 => null,
            var given => given,
        };
        var opens = change.Opens == null ? current.Opens : ParseOpens(change.Opens);
        var next = current with { Pick = pick, Max = max, Results = results, Correct = correct, Opens = opens };
        if (PollSettingsRules.Check(next, optionCount) is { } broken)
        {
            throw new BadRequestException(broken.StartsWith("correct:", StringComparison.Ordinal) ? $"{broken}, or 0 to clear it" : broken);
        }
        return next;
    }

    private static PollPick ParsePick(string value) => value switch
    {
        "one" => PollPick.One,
        "upTo" => PollPick.UpTo,
        _ => throw new BadRequestException("pick: must be one of 'one', 'upTo'"),
    };

    private static PollResults ParseResults(string value) => value switch
    {
        "live" => PollResults.Live,
        "afterAnswer" => PollResults.AfterAnswer,
        "afterClose" => PollResults.AfterClose,
        _ => throw new BadRequestException("results: must be one of 'live', 'afterAnswer', 'afterClose'"),
    };

    private static PollOpens ParseOpens(string value) => value switch
    {
        "onArrival" => PollOpens.OnArrival,
        "manual" => PollOpens.Manual,
        _ => throw new BadRequestException("opens: must be one of 'onArrival', 'manual'"),
    };
}
