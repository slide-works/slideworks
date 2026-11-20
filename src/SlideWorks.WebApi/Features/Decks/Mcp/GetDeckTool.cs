using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class GetDeckTool
{
    [McpServerTool(Name = "get_deck", ReadOnly = true)]
    [Description("Read a deck you own: its title, parts, sharing level and url, its look, and every slide with its number, layout, part, steps, revision, slots and notes. The look names the template, the version of it the deck is pinned to (templateVersion, null for a built-in template) and whether a newer published version is waiting (updateAvailable). Pass a slide's revision back when you update it. With summary, each slide is only its number, layout, part and headline, to read a long deck cheaply.")]
    public Task<CallToolResult> GetDeck(
        DeckReader reader,
        [Description("The deck's id, as create_deck returned it.")] string deck,
        [Description("True to get each slide as only {number, layout, part, headline}. Leave it out for every slide with its slots and notes.")] bool summary = false)
        => McpToolErrors.Run(async () =>
        {
            var result = await reader.GetForOwnerAsync(deck);
            return summary ? (object)GetDeckSummaryResult.From(result) : result;
        });
}
