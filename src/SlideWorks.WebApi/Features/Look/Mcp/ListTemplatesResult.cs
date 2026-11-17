namespace SlideWorks.WebApi.Features.Look.Mcp;

// list_templates: { templates: [...] }, every template in the catalogue. A wrapper object, because MCP clients refuse
// structured content that is an array.
public sealed record ListTemplatesResult(IReadOnlyList<ListTemplatesEntry> Templates);
