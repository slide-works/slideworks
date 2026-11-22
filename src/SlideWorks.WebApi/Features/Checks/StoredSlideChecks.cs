using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Checks;

// Every requested slide as stored, checked again against the deck as it is now: the template's layouts and fonts, the
// slide's surface and its images. Reads only; the caller has already checked access.
[RegisterPerRequest(For = typeof(IStoredSlideChecks))]
public sealed class StoredSlideChecks(AppDbContext db, ITemplates templates, IDeckImages images) : IStoredSlideChecks
{
    private const int WHERE_MAX = 80;
    private const string FIT_SUFFIX = "It fitted when it was added; the deck's look has changed since. Shorten it, or check it with validate_slide.";

    public async Task<IReadOnlyList<SlideFinding>> CheckAsync(string deckId, IReadOnlyList<int> slides, CancellationToken ct)
    {
        var positions = slides.Select(number => number - 1).ToList();
        var stored = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && positions.Contains(s.Position))
            .OrderBy(s => s.Position)
            .ToListAsync(ct);
        if (stored.Count == 0)
        {
            return [];
        }

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId, ct);
        var resolved = await templates.ForDeckAsync(deck, ct);
        var findings = new List<SlideFinding>();
        foreach (var slide in stored)
        {
            findings.AddRange(await CheckSlideAsync(deckId, resolved, slide));
        }
        return SlideFinding.Sorted(findings);
    }

    private async Task<IReadOnlyList<SlideFinding>> CheckSlideAsync(string deckId, ResolvedTemplate resolved, Slide slide)
    {
        var number = slide.Position + 1;
        var findings = new List<SlideFinding>();
        using var document = JsonDocument.Parse(slide.SlotsJson);
        var slots = document.RootElement;

        foreach (var warning in LayoutWarnings.For(resolved, number, slide.Layout))
        {
            findings.Add(Make(number, FindingRule.LayoutOutsideTemplate, slide.Layout, OutsideMessage(warning, number)));
        }

        IReadOnlyList<string> errors;
        if (resolved.FindLayout(slide.Layout) != null)
        {
            var (_, result) = await SlideChecks.ReportAsync(images, deckId, resolved, slide.Layout, slots, slide.Surface);
            errors = result.IsValid ? [] : result.Errors;
        }
        else
        {
            // The template does not have the layout; the built-in catalogue may, and the current font widths still apply.
            var definition = LayoutCatalogue.Find(slide.Layout);
            errors = definition is null ? [] : SlideValidator.Validate(definition, slots, resolved.FitWidths, slide.Surface).Errors;
        }

        foreach (var error in errors)
        {
            var colon = error.IndexOf(':');
            findings.Add(Make(number, FindingRule.Fit, colon > 0 ? error[..colon] : slide.Layout, $"{error.TrimEnd('.')}. {FIT_SUFFIX}"));
        }

        foreach (var (path, alt) in GenericAlt.Paths(slots, slide.Layout))
        {
            findings.Add(Make(number, FindingRule.GenericAlt, path, $"Alt text \"{alt}\" doesn't say what the picture shows; describe it in a few words."));
        }
        return findings;
    }

    // The finding carries the slide number itself, so the shared warning's "Slide N: " prefix goes; the fix is added.
    private static string OutsideMessage(string warning, int number)
    {
        var prefix = $"Slide {number}: ";
        var text = warning.StartsWith(prefix, StringComparison.Ordinal) ? warning[prefix.Length..] : warning;
        return $"{char.ToUpperInvariant(text[0])}{text[1..]}. Pick a layout from list_layouts, or change the look.";
    }

    private static SlideFinding Make(int slide, FindingRule rule, string where, string message)
        => new(slide, rule, SlideFinding.SeverityOf(rule), FindingSource.Stored, where.Length > WHERE_MAX ? where[..WHERE_MAX] : where, message);
}
