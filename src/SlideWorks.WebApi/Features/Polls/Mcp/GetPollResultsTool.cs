using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class GetPollResultsTool
{
    [McpServerTool(Name = "get_poll_results", ReadOnly = true)]
    [Description("Read a poll slide's results in a deck you can edit: its question and its current options, and for each time the deck went live, the rounds that were opened. Each round gives its number, the option texts it ran with (a closed round keeps its own, so rewording or removing options later never relabels it), how many answered, how many had joined (when it closed, or now for a round still open), the response rate (answered over joined, 0 to 1) and the count for each of that round's options, in its options' order. The last round of a live poll may still be open, with live counts. Totals only: never who picked what.")]
    public Task<CallToolResult> GetPollResults(
        PollResultsReader results,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide)
        => McpToolErrors.Run(() => results.ReadAsync(deck, slide));
}
