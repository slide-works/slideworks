using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class GetPollResultsTool
{
    [McpServerTool(Name = "get_poll_results", ReadOnly = true)]
    [Description("Read a poll slide's results in a deck you can edit: its question and options, and for each time the deck went live, the rounds that were opened and closed. Each round gives its number, how many answered, how many had joined when it closed, the response rate (answered over joined, 0 to 1) and the count for each option, in the options' order. A round still open isn't listed. Totals only: never who picked what.")]
    public Task<CallToolResult> GetPollResults(
        PollResultsReader results,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide)
        => McpToolErrors.Run(() => results.ReadAsync(deck, slide));
}
