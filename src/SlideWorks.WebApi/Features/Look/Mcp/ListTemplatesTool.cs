using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_templates (L7): the template catalogue, read through ITemplateCatalogue so nothing here names a template.
[McpServerToolType]
public sealed class ListTemplatesTool
{
    public const string TOOL = "list_templates";

    [McpServerTool(Name = TOOL, ReadOnly = true)]
    [Description("List the templates a deck can use with create_deck or set_look: each one's name, label, purpose, the layouts it allows, its chrome defaults, the outline and house style to write the deck to, its type, whether it is the default, and the palette it locks (null when any palette goes). Pick the template whose purpose fits the talk, then write to its outline.")]
    public Task<CallToolResult> ListTemplates(ITemplateCatalogue templates)
        => McpToolErrors.Run(() => Task.FromResult(new ListTemplatesResult(templates.All.Select(ListTemplatesEntry.From).ToList())));
}
