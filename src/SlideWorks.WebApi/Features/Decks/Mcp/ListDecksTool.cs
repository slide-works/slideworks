using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class ListDecksTool
{
    [McpServerTool(Name = "list_decks", ReadOnly = true)]
    [Description("List the decks of the person you act for, plus the decks shared with them with write access (access write), most recently changed first: each one's id (pass it as deck to other tools), title, subtitle, slide count, sharing level, player url and when it last changed.")]
    public Task<CallToolResult> ListDecks(DeckQueries queries)
        => McpToolErrors.Run(queries.ListForAgentAsync);
}
