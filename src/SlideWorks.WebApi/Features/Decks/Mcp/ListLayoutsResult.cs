using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// list_layouts: { layouts, $defs }. $defs holds the icon, emphasis and callout schemas that every layout would
// otherwise repeat. A $ref inside a layout schema is against this response. A wrapper object, because MCP clients
// refuse structured content that is an array. $defs is left off when nothing is shared.
public sealed record ListLayoutsResult(
    IReadOnlyList<LayoutInfo> Layouts,
    [property: JsonPropertyName("$defs")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonObject? Defs = null);
