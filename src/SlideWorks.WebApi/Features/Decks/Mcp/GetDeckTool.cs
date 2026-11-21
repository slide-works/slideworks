using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class GetDeckTool
{
    [McpServerTool(Name = "get_deck", ReadOnly = true)]
    [Description("Read a deck you own: its title, parts, sharing level and url, its look, its Q&A settings (qa) and its reaction sessions (reactions), and every slide with its number, layout, part, steps, revision, slots and notes. detail \"summary\" returns each slide's number, layout, part and headline only. The look names the template, the version of it the deck is pinned to (templateVersion, null for a built-in template) and whether a newer published version is waiting (updateAvailable). Pass a slide's revision back when you update it.")]
    public Task<CallToolResult> GetDeck(
        DeckReader reader,
        [Description("The deck's id, as create_deck returned it.")] string deck,
        [Description("\"full\" (the default) includes every slide's slots. \"summary\" returns number, layout, part and headline.")] string? detail = null)
        => McpToolErrors.Run(() => reader.GetForOwnerAsync(deck, detail switch
        {
            null or "" or "full" => false,
            "summary" => true,
            _ => throw new BadRequestException("detail: must be \"full\" or \"summary\""),
        }));
}
