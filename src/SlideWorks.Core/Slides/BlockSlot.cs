using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// A slot that holds one inline block, such as bullets' callout or a card's chips.
public sealed class BlockSlot(InlineBlock block) : ISlotType
{
    public InlineBlock Block { get; } = block;

    private ISlotType Type => Block.Type ?? throw new InvalidOperationException($"Block '{Block.Name}' is not set by agents");

    public string Expected => Type.Expected;

    public string Kind => Block.Name;

    public JsonObject ToJsonSchema()
    {
        var schema = Type.ToJsonSchema();
        schema["description"] = $"Inline block '{Block.Name}': {Block.Description}";
        return schema;
    }

    public void Validate(JsonElement value, string path, SlotValidation validation) =>
        Type.Validate(value, path, validation);
}
