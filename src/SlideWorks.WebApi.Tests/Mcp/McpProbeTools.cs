using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Tests.Mcp;

// Tools that exist only on McpToolErrorsTests' host, added through ConfigureTestServices, to drive McpToolErrors and the
// agent's identity through the real transport before any production tool exists.
[McpServerToolType]
public sealed class McpProbeTools
{
    [McpServerTool(Name = "probe_whoami")]
    public Task<CallToolResult> WhoAmI(ICurrentActor actor)
        => McpToolErrors.Run(() => Task.FromResult(new
        {
            actor.PresenterId,
            actor.Email,
            actor.Name,
            actor.Kind,
            actor.Via,
        }));

    [McpServerTool(Name = "probe_throw")]
    public Task<CallToolResult> Throw(string kind)
        => McpToolErrors.Run<int>(() => kind switch
        {
            "not_found" => throw new NotFoundException("Deck not found"),
            "bad_request" => throw new BadRequestException("slots.title: required"),
            "conflict" => throw new ConflictException("The slide changed since revision 3."),
            _ => Task.FromResult(42),
        });
}
