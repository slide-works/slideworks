namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_templates: { templates: [...] }, every template the caller may pick: the built-ins, then their own and the ones shared with them. A wrapper object, because MCP clients refuse
// structured content that is an array.
public sealed record ListTemplatesResult(IReadOnlyList<ListTemplatesEntry> Templates);
