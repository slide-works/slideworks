using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_palettes (L7): the palette catalogue, filtered. size matches the swatch's length, and colour matches one of the
// palette's words, ignoring case. No match is an empty list, not an error.
[McpServerToolType]
public sealed class ListPalettesTool
{
    public const string TOOL = "list_palettes";

    [McpServerTool(Name = TOOL, ReadOnly = true)]
    [Description("List the palettes a deck can use with create_deck or set_look: each one's name, label, the words it answers to, its swatch, every colour token, whether it is the default, and its contrast (pass, and the pair with the least headroom). Filter by size and colour to narrow the list; no match returns an empty list.")]
    public Task<CallToolResult> ListPalettes(
        IPaletteCatalogue palettes,
        [Description("Only palettes whose swatch has this many colours.")] int? size = null,
        [Description("Only palettes described by this word, such as a colour name or a mood. Case doesn't matter.")] string? colour = null)
        => McpToolErrors.Run(() => Task.FromResult(new ListPalettesResult(palettes.All
            .Where(palette => size is null || palette.Swatch.Count == size)
            .Where(palette => colour is null || palette.Words.Contains(colour.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(ListPalettesEntry.From)
            .ToList())));
}
