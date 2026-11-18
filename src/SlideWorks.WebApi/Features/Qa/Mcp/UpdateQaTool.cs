using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Qa.Mcp;

[McpServerToolType]
public sealed class UpdateQaTool
{
    [McpServerTool(Name = QaSettingsCommands.TOOL, Idempotent = true)]
    [Description("Change a deck's audience Q&A settings, live or not. You must be able to edit the deck. Give only the settings to change; the others keep their value. Without slide, questions is \"open\", \"closed\" (the list and upvotes stay, no new questions) or \"off\" (no Questions tab and no round); review true makes a new question wait for the presenter's approval; names false removes the name from the ask sheet; reactions turns the emoji reactions on or off; show_on is \"anySlide\" or \"qaSlide\" (where a question the presenter shows appears). With slide (its 1-based number), only questions (\"open\" or \"off\") and reactions are taken, and they set that slide's own switch; the other settings are refused by name. A slide switch can only turn off what the deck has on, but one for something the deck has off is stored. Returns the settings after the change as a sentence and as fields. The change shows in the deck's activity.")]
    public Task<CallToolResult> UpdateQa(
        QaAgentAccess access,
        [Description("The deck's id.")] string deck,
        [Description("\"open\", \"closed\" or \"off\". With slide: \"open\" or \"off\".")] string? questions = null,
        [Description("True makes a new question wait for approval. Not with slide.")] bool? review = null,
        [Description("False removes \"Show my name\" and refuses a name. Not with slide.")] bool? names = null,
        [Description("Whether guests can send reactions. With slide: that slide's switch.")] bool? reactions = null,
        [Description("\"anySlide\" or \"qaSlide\". Not with slide.")] string? show_on = null,
        [Description("A slide's 1-based number, to set that slide's switch instead of the deck's.")] int? slide = null)
        => McpToolErrors.Run(() => access.UpdateAsync(deck, new UpdateQaChange(questions, review, names, reactions, show_on, slide)));
}
