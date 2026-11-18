using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class ListBlocksTool
{
    [McpServerTool(Name = "list_blocks", ReadOnly = true)]
    [Description("List the small blocks that sit inside a layout's slots, such as callout and chip: each one's name, what it is for, whether you set it (journey is drawn from the deck's parts), and the JSON Schema of its value. It also returns the icon names an icon slot takes. A link is an http or https address.")]
    public Task<CallToolResult> ListBlocks()
        => McpToolErrors.Run(() => Task.FromResult(new ListBlocksResult(LayoutCatalogue.Blocks.Select(BlockInfo.From).ToList(), IconCatalogue.Names)));
}
