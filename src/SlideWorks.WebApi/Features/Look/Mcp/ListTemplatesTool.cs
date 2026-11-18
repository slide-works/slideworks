using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_templates (L7): the templates the caller may pick, read through ITemplates so nothing here names a template.
[McpServerToolType]
public sealed class ListTemplatesTool
{
    public const string TOOL = "list_templates";

    [McpServerTool(Name = TOOL, ReadOnly = true)]
    [Description("List the templates a deck can use with create_deck or set_look: each one's name, label, purpose, the layouts it allows, its chrome defaults, the outline and house style to write the deck to, its type, whether it is the default, and the palette it locks (null when any palette goes). Each entry also has ref (pass it as the template argument of other tools), source (builtin, yours or shared with you), version (its latest published version, null on a built-in) and owner (who shared it, null on a built-in and on yours). Pick the template whose purpose fits the talk, then write to its outline.")]
    public Task<CallToolResult> ListTemplates(ITemplates templates, ICurrentActor actor)
        => McpToolErrors.Run(async () => new ListTemplatesResult((await templates.ListForAsync(actor, CancellationToken.None)).Select(t => ListTemplatesEntry.From(t, actor.PresenterId)).ToList()));
}
