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
    [Description("Read a deck you own: its title, parts, sharing level and url, its look, its Q&A settings (qa) and its reaction sessions (reactions), and every slide with its number, layout, part, steps, revision, slots and notes. detail \"summary\" trims the slides to number, layout, part and headline only and keeps everything else, qa and reactions included. The look names the template, the version of it the deck is pinned to (templateVersion, null for a built-in template) and whether a newer published version is waiting (updateAvailable). Pass a slide's revision back when you update it. While translations are on, translations gives the primary language and each language's line counts (lines, current, stale, missing).")]
    public Task<CallToolResult> GetDeck(
        DeckReader reader,
        [Description("The deck's id, as create_deck returned it.")] string deck,
        [Description("\"full\" (the default) includes every slide's slots. \"summary\" returns number, layout, part and headline.")] string? detail = null,
        [Description("A language on the deck's translations list (see translations). Each slide then also lists its lines: every translatable line's path, its primary text, the stored translation (null when missing) and its state, \"current\", \"stale\" or \"missing\". The slots stay in the primary language, and the primary language itself lists no lines. Translate the lines with translate_slides.")] string? language = null)
        => McpToolErrors.Run(() => reader.GetForEditorAsync(deck, detail switch
        {
            null or "" or "full" => false,
            "summary" => true,
            _ => throw new BadRequestException("detail: must be \"full\" or \"summary\""),
        }, language));
}
