using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;

namespace SlideWorks.WebApi.Features.Checks;

// check_deck: the stored findings for the slides, plus (when measure is true) the findings each slide's render page measures.
// Editors only; anyone else gets "Deck not found". A slide that fails to render is a renderFailed finding and the run goes on;
// no renderer, or a render that times out, stops the measuring and keeps what was collected.
[RegisterPerRequest]
public class DeckChecks(AppDbContext db, ICurrentActor actor, IDeckAccess access, IRenderEngine engine, IStoredSlideChecks stored, ILogger<DeckChecks> logger)
{
    public async Task<DeckChecksResult> CheckAsync(string deckId, IReadOnlyList<int>? slides, bool measure)
    {
        var result = await RunAsync(deckId, slides, measure);

        // One line per call is the Measure (problems per deck, first call against last): counts only, never a token.
        var rules = string.Join(",", result.Findings.GroupBy(f => f.Rule).Select(g => $"{JsonNamingPolicy.CamelCase.ConvertName(g.Key.ToString())}={g.Count()}"));
        logger.LogInformation(
            "Checked deck {DeckId}: {Slides} slides, {Errors} errors, {Warnings} warnings, {Notes} notes, measured {Measured}; by rule {Rules}",
            deckId,
            result.Slides.Count,
            result.Findings.Count(f => f.Severity == FindingSeverity.Error),
            result.Findings.Count(f => f.Severity == FindingSeverity.Warning),
            result.Findings.Count(f => f.Severity == FindingSeverity.Note),
            result.Measured,
            rules);
        return result;
    }

    private async Task<DeckChecksResult> RunAsync(string deckId, IReadOnlyList<int>? slides, bool measure)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        var count = await db.Slides.AsNoTracking().CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        var numbers = slides is { Count: > 0 } ? slides.Distinct().ToList() : Enumerable.Range(1, count).ToList();
        if (numbers.Any(n => n < 1 || n > count))
        {
            throw new BadRequestException($"slides: no slide {numbers.First(n => n < 1 || n > count)}");
        }

        var findings = new List<SlideFinding>(await stored.CheckAsync(deckId, numbers, CancellationToken.None));
        if (!measure)
        {
            return new DeckChecksResult(deckId, numbers, SlideFinding.Sorted(findings), false, null);
        }

        for (var i = 0; i < numbers.Count; i++)
        {
            var number = numbers[i];
            try
            {
                findings.AddRange((await engine.RenderSlideAsync(deckId, number, CancellationToken.None)).Findings);
            }
            catch (RenderingFailedException error)
            {
                findings.Add(new SlideFinding(
                    number,
                    FindingRule.RenderFailed,
                    SlideFinding.SeverityOf(FindingRule.RenderFailed),
                    FindingSource.Measured,
                    "slide",
                    $"This slide didn't render: {error.Reason}. Fix what the reason names, then check again."));
            }
            catch (Exception error) when (error is RendererUnavailableException or TimeoutException)
            {
                var stoppedAt = $"Rendering stopped at slide {number}, so {Unmeasured(numbers.Skip(i).ToList())} the stored checks.";
                return new DeckChecksResult(deckId, numbers, SlideFinding.Sorted(findings), false, stoppedAt);
            }
        }

        return new DeckChecksResult(deckId, numbers, SlideFinding.Sorted(findings), true, null);
    }

    // "slide 5 has only", "slides 4, 5 and 9 have only", or "slides 4–12 have only" for a run of three or more.
    private static string Unmeasured(IReadOnlyList<int> left)
    {
        if (left.Count == 1)
        {
            return $"slide {left[0]} has only";
        }

        var contiguous = left.Count >= 3 && left.Zip(left.Skip(1), (a, b) => b == a + 1).All(x => x);
        var names = contiguous ? $"{left[0]}–{left[^1]}" : $"{string.Join(", ", left.Take(left.Count - 1))} and {left[^1]}";
        return $"slides {names} have only";
    }
}
