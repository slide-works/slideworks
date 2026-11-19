using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

// A fit check that writes nothing. It still needs the deck: the template's font widths are what the fit uses.
[McpServerToolType]
public sealed class ValidateSlideTool
{
    public const string NAME = "validate_slide";

    [McpServerTool(Name = NAME, ReadOnly = true)]
    [Description("Check whether a slide would fit, without creating or changing one. Pass the deck, the layout and the slots as you would to add_slide. Returns ok, the step count when it fits, and the fit of each region: its height, its budget, and each item's height. A slide that cannot render comes back with ok false and the same error add_slide would give.")]
    public Task<CallToolResult> ValidateSlide(
        ICurrentActor actor,
        IDeckAccess access,
        ITemplates templates,
        IDeckImages images,
        AppDbContext db,
        [Description("The deck's id. The check uses that deck's template and font widths, and writes nothing.")] string deck,
        [Description("The layout's name, as list_layouts names it.")] string layout,
        [Description("The layout's slots as a JSON object.")] JsonObject slots,
        [Description("\"light\" or \"dark\", for a layout whose surface is 'either'. Leave it out for the layout's own surface.")] string? surface = null)
        => McpToolErrors.Run(() => CheckAsync(actor, access, templates, images, db, deck, layout, slots, surface));

    private static async Task<ValidateSlideResult> CheckAsync(
        ICurrentActor actor,
        IDeckAccess access,
        ITemplates templates,
        IDeckImages images,
        AppDbContext db,
        string deck,
        string layout,
        JsonObject slots,
        string? surface)
    {
        if (!await access.CanViewAsync(deck, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        var stored = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deck);
        var template = await templates.ForDeckAsync(stored, CancellationToken.None);
        var element = JsonSerializer.SerializeToElement(slots);
        var (definition, result) = await SlideChecks.ReportAsync(images, deck, template, layout, element, surface);
        return new ValidateSlideResult(result.IsValid, result.IsValid ? SlideSteps.Count(definition, element) : null, result.IsValid ? null : result.Error, result.Regions);
    }
}
