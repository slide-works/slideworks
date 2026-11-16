using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// A small block that sits inside a slot (a callout, a chip). Journey has no type: the player renders
// it from the deck's parts, and an agent never sets it.
public sealed class InlineBlock(string name, string description, ISlotType? type)
{
    public string Name { get; } = name;

    public string Description { get; } = description;

    public ISlotType? Type { get; } = type;

    public bool SetByAgent => Type is not null;

    public JsonObject ToJsonSchema()
    {
        var schema = new JsonObject
        {
            ["$schema"] = LayoutDefinition.JSON_SCHEMA_DIALECT,
            ["title"] = Name,
            ["description"] = Description,
        };
        if (Type is null)
        {
            schema["not"] = new JsonObject();
            return schema;
        }

        foreach (var (key, value) in Type.ToJsonSchema())
        {
            schema[key] = value?.DeepClone();
        }

        return schema;
    }
}
