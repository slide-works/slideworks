using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts (K6, L7): every layout, or with template only the ones that template allows. An unknown template is a tool
// error with the L3 text, and so is one the caller may not pick: the same rule as create_deck, so the answer never tells
// whether such a template exists.
[McpServerToolType]
public sealed class ListLayoutsTool
{
    [McpServerTool(Name = "list_layouts", ReadOnly = true)]
    [Description("List the slide layouts you can pass to add_slide: each one's name, what it is for, its surface (light, dark or either; with either, add_slide's surface picks), what its steps reveal, the JSON Schema of its slots, how many typical items each list or table slot fits, and custom (true for a layout the template brings itself, listed after the built-ins). The icon, emphasis and callout schemas are shared in $defs; a $ref is against this response, not against a schema copied out on its own. detail \"compact\" leaves the schema out: each slot is a short {name, kind, required, max} entry (with values on an enum), and the icon names and the blocks are listed once, not per layout. Give a deck to list the layouts of the template version that deck is pinned to, or a template to list only the layouts it allows. Slot text is plain text: no Markdown or HTML.")]
    public Task<CallToolResult> ListLayouts(
        ITemplates templates,
        ICurrentActor actor,
        IDeckAccess access,
        AppDbContext db,
        [Description("A template's name, as list_templates or get_deck names it, to list only its layouts. Leave it out for every layout.")] string? template = null,
        [Description("\"full\" (the default) includes each layout's JSON Schema. \"compact\" returns slots, limits and capacity only.")] string? detail = null,
        [Description("A deck's id, to list the layouts of that deck's pinned template version, as add_slide takes them. You must be able to edit the deck.")] string? deck = null)
        => McpToolErrors.Run(async () =>
        {
            var layouts = (await LayoutsOfAsync(templates, actor, access, db, template, deck)).ToList();
            return Detail(detail) switch
            {
                "compact" => (object)CompactListLayoutsResult.From(layouts),
                _ => Full(layouts),
            };
        });

    private static ListLayoutsResult Full(IReadOnlyList<ListedLayout> layouts)
    {
        var infos = layouts.Select(LayoutInfo.From).ToList();
        var defs = SharedLayoutSchemas.Share(infos.Select(info => info.Schema));
        return new ListLayoutsResult(infos, defs.Count == 0 ? null : defs);
    }

    private static string Detail(string? detail) => detail switch
    {
        null or "" or "full" => "full",
        "compact" => "compact",
        _ => throw new BadRequestException("detail: must be \"full\" or \"compact\""),
    };

    // A template's layouts are the ones it names and the live room's, each as the template itself finds it, in the
    // catalogue's order with any layout of the template's own after them, and last its approved custom layouts. A deck
    // answers for its pinned version to anyone who may edit it, whatever the template's sharing or archive state; a deck
    // the caller cannot edit is not found, as everywhere.
    private static async Task<IEnumerable<ListedLayout>> LayoutsOfAsync(ITemplates templates, ICurrentActor actor, IDeckAccess access, AppDbContext db, string? name, string? deckId)
    {
        if (deckId is not null)
        {
            var found = await db.Decks.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deckId);
            if (found is null || !await access.CanEditAsync(deckId, actor))
            {
                throw new NotFoundException(DeckReader.NOT_FOUND);
            }
            return Of(await templates.ForDeckAsync(found, CancellationToken.None));
        }
        if (name is null)
        {
            return LayoutCatalogue.Layouts.Select(layout => new ListedLayout(layout));
        }
        var picked = (await templates.ListForAsync(actor, CancellationToken.None)).FirstOrDefault(t => string.Equals(t.Ref, name, StringComparison.Ordinal))
            ?? throw new BadRequestException(LookRules.UnknownTemplate(name));
        // The picker's list carries no custom layouts; the template's latest version has them.
        return Of(await templates.FindAsync(picked.Ref, null, CancellationToken.None) ?? picked);
    }

    private static IEnumerable<ListedLayout> Of(ResolvedTemplate template)
    {
        // One list of names with the refusal (ResolvedTemplate.AllowedLayoutNames); an approved custom layout is listed once, as custom.
        var builtIn = template.ListedLayoutNames()
            .Select(template.FindLayout)
            .OfType<LayoutDefinition>()
            .Select(layout => new ListedLayout(layout));
        var custom = template.CustomLayouts
            .Select(c => new ListedLayout(c.Layout.Definition, $"{template.Ref}@{template.Version}/{c.AssetId}"));
        return builtIn.Concat(custom);
    }
}
