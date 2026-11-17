using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// set_look (L7, L8): the same LookCommands path as PUT /api/decks/{id}/look, so the rules and the error texts match.
// logo and chrome come in as raw JSON so that a null (remove the logo, reset a chrome key) differs from leaving it out.
[McpServerToolType]
public sealed class SetLookTool
{
    [McpServerTool(Name = LookCommands.TOOL)]
    [Description("Change a deck's look: its template, palette, logo, draft mark or chrome. Give only what changes. A template that locks its palette refuses another palette, and switching to it switches the palette. Returns the look in get_deck's terms and a warning for each slide whose layout isn't in the template (it still renders; consider changing it). The change shows in the deck's activity.")]
    public Task<CallToolResult> SetLook(
        LookCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("A template's name, as list_templates gives it.")] string? template = null,
        [Description("A palette's name, as list_palettes gives it.")] string? palette = null,
        [Description("The logo: the id of a completed upload_image on this deck, or null to remove the logo.")] JsonElement? logo = null,
        [Description("true to mark the deck as a draft, false to clear the mark.")] bool? draft = null,
        [Description("Chrome overrides, any of { \"tracker\": true|false, \"footer\": \"author\"|\"date\"|\"off\", \"progress\": true|false }. A key set to null goes back to the template's default; a key left out keeps its current value.")] JsonElement? chrome = null)
        => McpToolErrors.Run(() => commands.SetFromAgentAsync(deck, new LookChangeRequest(template, palette, logo ?? default, draft, chrome ?? default).ToChange()));
}
