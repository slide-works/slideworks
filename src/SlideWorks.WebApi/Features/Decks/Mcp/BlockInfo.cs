using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One inline block in list_blocks: its name, what it is for, whether an agent sets it in a slot (journey is rendered from
// the deck's parts instead), and the JSON Schema of its value.
public sealed record BlockInfo(string Name, string Description, bool SetByAgent, JsonObject Schema)
{
    public static BlockInfo From(InlineBlock block) => new(block.Name, block.Description, block.SetByAgent, block.ToJsonSchema());
}
