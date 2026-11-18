using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
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
    [Description("List the slide layouts you can pass to add_slide: each one's name, what it is for, its surface (light, dark or either; with either, add_slide's surface picks), what its steps reveal, and the JSON Schema of its slots. Give a template to list only the layouts it allows. Slot text is plain text: no Markdown or HTML.")]
    public Task<CallToolResult> ListLayouts(
        ITemplates templates,
        ICurrentActor actor,
        [Description("A template's name, as list_templates or get_deck names it, to list only its layouts. Leave it out for every layout.")] string? template = null)
        => McpToolErrors.Run(async () => new ListLayoutsResult((await LayoutsOfAsync(templates, actor, template)).Select(LayoutInfo.From).ToList()));

    // A template's layouts are the ones it names and the live room's, each as the template itself finds it, in the
    // catalogue's order with any layout of the template's own after them.
    private static async Task<IEnumerable<LayoutDefinition>> LayoutsOfAsync(ITemplates templates, ICurrentActor actor, string? name)
    {
        if (name is null)
        {
            return LayoutCatalogue.Layouts;
        }
        var template = (await templates.ListForAsync(actor, CancellationToken.None)).FirstOrDefault(t => string.Equals(t.Ref, name, StringComparison.Ordinal))
            ?? throw new BadRequestException(LookRules.UnknownTemplate(name));
        var own = template.Template.Layouts.Where(layout => LayoutCatalogue.Layouts.All(known => !string.Equals(known.Name, layout, StringComparison.Ordinal)));
        return LayoutCatalogue.Layouts
            .Where(layout => template.Template.Allows(layout.Name))
            .Select(layout => layout.Name)
            .Concat(own)
            .Select(template.FindLayout)
            .OfType<LayoutDefinition>();
    }
}
