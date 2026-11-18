using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Qa.Mcp;

// What an agent may do with a deck's Q&A (Q10): change its settings, and read its questions (QuestionsReports asks
// RequireEditorAsync). It needs edit access to the deck, not the right to present, because an agent acts for its owner and
// never presents; a deck the caller can't edit is 404 "Deck not found". Live or not. Every check comes before IQaSettings
// is called, so a refused call changes nothing. The change is IQaSettings' and so are its activity entry (logged under the
// tool's name for an agent), its refresh and its publish; this adds none. Per request.
[RegisterPerRequest]
public class QaAgentAccess(IQaSettings settings, AppDbContext db, IDeckAccess access, ICurrentActor actor)
{
    public async Task RequireEditorAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }

    public async Task<UpdateQaResult> UpdateAsync(string deckId, UpdateQaChange change)
    {
        await RequireEditorAsync(deckId);
        return change.Slide is { } slide ? await UpdateSlideAsync(deckId, slide, change) : await UpdateDeckAsync(deckId, change);
    }

    private async Task<UpdateQaResult> UpdateDeckAsync(string deckId, UpdateQaChange change)
    {
        var patch = new QaSettingsPatch(ParseQuestions(change.Questions), change.Review, change.Names, change.Reactions, ParseShowOn(change.ShowOn));
        if (patch == new QaSettingsPatch(null, null, null, null, null))
        {
            throw new BadRequestException("give at least one setting to change: questions, review, names, reactions or show_on, or slide with questions or reactions");
        }
        var after = await settings.UpdateAsync(deckId, patch);
        return new UpdateQaResult(Sentence(after), after, null, null);
    }

    private async Task<UpdateQaResult> UpdateSlideAsync(string deckId, int slide, UpdateQaChange change)
    {
        foreach (var (name, given) in new[] { ("review", change.Review != null), ("names", change.Names != null), ("show_on", change.ShowOn != null) })
        {
            if (given)
            {
                throw new BadRequestException($"{name}: a slide only has questions and reactions switches; leave out slide to change the deck's {name}");
            }
        }
        var questions = change.Questions switch
        {
            null => (bool?)null,
            "open" => true,
            "off" => false,
            _ => throw new BadRequestException("questions: with slide, must be one of 'open', 'off'"),
        };
        if (questions == null && change.Reactions == null)
        {
            throw new BadRequestException("give questions or reactions to change on the slide");
        }
        var count = await db.Slides.CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        if (slide < 1 || slide > count)
        {
            throw new BadRequestException(count == 0 ? "slide: the deck has no slides" : $"slide: must be between 1 and {count}");
        }
        var after = await settings.SetSlideAsync(deckId, slide, new QaSlidePatch(questions, change.Reactions));
        return new UpdateQaResult($"slide {slide}: questions {OnOff(after.Questions)} · reactions {OnOff(after.Reactions)}", null, slide, after);
    }

    private static QaQuestions? ParseQuestions(string? value) => value switch
    {
        null => null,
        "open" => QaQuestions.Open,
        "closed" => QaQuestions.Closed,
        "off" => QaQuestions.Off,
        _ => throw new BadRequestException("questions: must be one of 'open', 'closed', 'off'"),
    };

    private static QaShowOn? ParseShowOn(string? value) => value switch
    {
        null => null,
        "anySlide" => QaShowOn.AnySlide,
        "qaSlide" => QaShowOn.QaSlide,
        _ => throw new BadRequestException("show_on: must be one of 'anySlide', 'qaSlide'"),
    };

    // "questions open · review first · names allowed · reactions on · shown over any slide"
    private static string Sentence(QaSettings now) => string.Join(
        " · ",
        $"questions {now.Questions.ToString().ToLowerInvariant()}",
        now.Review ? "review first" : "no review",
        now.Names ? "names allowed" : "names off",
        $"reactions {OnOff(now.Reactions)}",
        now.ShowOn == QaShowOn.QaSlide ? "shown on the Q&A slide only" : "shown over any slide");

    private static string OnOff(bool on) => on ? "on" : "off";
}
