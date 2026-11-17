using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class UpdatePollTool
{
    [McpServerTool(Name = PollSettingsCommands.UPDATE_TOOL, Idempotent = true)]
    [Description("Change a poll slide's settings in a deck you can edit. Give only the settings to change; the others keep their value. The question and the options are the slide's slots (add_slide and update_slide); these are not. Refused while a round of the poll is open: close it first. Returns every setting after the change: pick, max, results, correct, opens and privacy.")]
    public Task<CallToolResult> UpdatePoll(
        PollSettingsCommands commands,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide,
        [Description("\"one\" (a tap sends the answer) or \"upTo\" (the guest picks up to max options and then sends). Switching to \"one\" clears max.")] string? pick = null,
        [Description("With pick \"upTo\": how many options a guest may pick, 2 up to the number of options. Leave it out with pick \"one\".")] int? max = null,
        [Description("When the room sees the counts: \"live\", \"afterAnswer\" (each guest once they have answered) or \"afterClose\".")] string? results = null,
        [Description("The correct answer as a 1-based option number, or 0 to clear it. Guests never see it before the presenter shows the answer.")] int? correct = null,
        [Description("When the first round opens: \"onArrival\" (when the presenter reaches the slide) or \"manual\" (when the presenter presses Open).")] string? opens = null)
        => McpToolErrors.Run(() => commands.UpdateAsync(deck, slide, new PollSettingsChange(pick, max, results, correct, opens)));
}
