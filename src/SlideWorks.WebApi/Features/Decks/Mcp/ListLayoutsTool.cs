using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class ListLayoutsTool
{
    [McpServerTool(Name = "list_layouts", ReadOnly = true)]
    [Description("List the slide layouts you can pass to add_slide: each one's name, what it is for, whether it is a dark slide, which list can reveal one item per step, and the JSON Schema of its slots. Slot text is plain text: no Markdown or HTML.")]
    public Task<CallToolResult> ListLayouts()
        => McpToolErrors.Run(() => Task.FromResult(new ListLayoutsResult(LayoutCatalogue.Layouts.Select(LayoutInfo.From).ToList())));
}
