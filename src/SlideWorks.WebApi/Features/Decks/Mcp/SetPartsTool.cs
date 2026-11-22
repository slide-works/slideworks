using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// set_parts (SW-555): replaces the deck's parts list, checked by the same code as create_deck's parts.
[McpServerToolType]
public sealed class SetPartsTool
{
    [McpServerTool(Name = PartsCommands.TOOL)]
    [Description("Replace a deck's whole list of parts after it was made (the same rules as create_deck's parts). Slides keep their part numbers, so a list shorter than the highest part a slide uses is refused and names those slides: move them with update_slide first. You must own the deck. The change shows in the deck's activity. Send every part you want to keep: get_deck shows the current ones.")]
    public Task<CallToolResult> SetParts(
        PartsCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The deck's parts, up to 8, replacing the current list ([] clears it). Each is a name of up to 40 characters, or {name, short, caption, mark}: short (16) shows on the tracker, caption (24) under the journey stop, and a mark of 1–2 characters such as 'A' makes an unnumbered section, shown on neither. A part with a mark must come after every numbered part.")] JsonElement[]? parts)
        => McpToolErrors.Run(async () => new SetPartsResult(await commands.SetAsync(deck, parts, PartsCommands.TOOL)));
}

// set_parts's result: the deck's parts after the change.
public sealed record SetPartsResult([property: JsonPropertyName("parts")] IReadOnlyList<DeckPart> Parts);
