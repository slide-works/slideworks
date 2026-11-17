using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class AddPollOptionTool
{
    [McpServerTool(Name = PollSettingsCommands.ADD_OPTION_TOOL)]
    [Description("Append an option to a poll slide in a deck you can edit, at the end, so the other options keep their place. Allowed while a round is open: the phones and the presenter's screen show the new option at once. A poll has at most 8 options, each up to 60 characters. Returns the options in order.")]
    public Task<CallToolResult> AddPollOption(
        PollSettingsCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide,
        [Description("The new option's text, plain text with no Markdown.")] string text)
        => McpToolErrors.Run(() => commands.AddOptionAsync(deck, slide, text));
}
