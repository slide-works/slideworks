using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts (K6, L7): every layout, or with template only the ones that template allows. An unknown template is a tool
// error with the L3 text.
[McpServerToolType]
public sealed class ListLayoutsTool
{
    [McpServerTool(Name = "list_layouts", ReadOnly = true)]
    [Description("List the slide layouts you can pass to add_slide: each one's name, what it is for, whether it is a dark slide, which list can reveal one item per step, and the JSON Schema of its slots. Give a template to list only the layouts it allows. Slot text is plain text: no Markdown or HTML.")]
    public Task<CallToolResult> ListLayouts(
        ITemplateCatalogue templates,
        [Description("A template's name, as list_templates or get_deck names it, to list only its layouts. Leave it out for every layout.")] string? template = null)
        => McpToolErrors.Run(() => Task.FromResult(new ListLayoutsResult(LayoutsOf(templates, template).Select(LayoutInfo.From).ToList())));

    private static IEnumerable<LayoutDefinition> LayoutsOf(ITemplateCatalogue templates, string? name)
    {
        if (name is null)
        {
            return LayoutCatalogue.Layouts;
        }
        var template = templates.Find(name) ?? throw new BadRequestException(LookRules.UnknownTemplate(name));
        return LayoutCatalogue.Layouts.Where(layout => template.Allows(layout.Name));
    }
}
