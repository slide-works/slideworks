using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Sharing.Mcp;

// The level comes in as a string and SharingService matches it against the wire names, because the SDK binds tool
// arguments with its own serializer, not the app's camelCase enum converter. viewers comes in as raw JSON so that
// SharingService refuses anything but a list of strings with its own message, not the SDK's generic binding error.
[McpServerToolType]
public sealed class SetSharingTool
{
    [McpServerTool(Name = SharingService.TOOL, Idempotent = true)]
    [Description("Change who can open a deck you own: \"private\" (only you), \"link\" (anyone with the link, signed in or not; they can view and present, never edit) or \"named\" (only the people whose emails are in viewers, after they sign in). Give viewers to replace the list; leave it out to keep it. The change shows in the deck's activity. Returns the level, the viewers and the share link.")]
    public Task<CallToolResult> SetSharing(
        SharingService sharing,
        [Description("The deck's id.")] string deck,
        [Description("\"private\", \"link\" or \"named\".")] string level,
        [Description("The named viewers' email addresses, up to 50. They only get in while the level is \"named\".")] JsonElement? viewers = null)
        => McpToolErrors.Run(() => sharing.SetFromAgentAsync(deck, level, viewers));
}
