using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// One layout: its frozen name, a one-line description for agents, whether it is a dark slide, its
// slot schema, and the list an agent can reveal one item per step.
public sealed class LayoutDefinition
{
    public const string JSON_SCHEMA_DIALECT = "https://json-schema.org/draft/2020-12/schema";

    private readonly Action<JsonElement, string, SlotValidation>? _rules;

    public LayoutDefinition(
        string name,
        string description,
        bool dark,
        IReadOnlyList<SlotField> fields,
        string? stepsFrom = null,
        Action<JsonElement, string, SlotValidation>? rules = null)
    {
        Name = name;
        Description = description;
        Dark = dark;
        Slots = new ObjectSlot(fields);
        StepsFrom = stepsFrom;
        _rules = rules;
    }

    public string Name { get; }

    public string Description { get; }

    public bool Dark { get; }

    public ObjectSlot Slots { get; }

    // The list field whose items appear one per step when reveal is "one-by-one" (K2), or null when
    // the layout has no steps.
    public string? StepsFrom { get; }

    public JsonObject ToJsonSchema()
    {
        var schema = new JsonObject
        {
            ["$schema"] = JSON_SCHEMA_DIALECT,
            ["title"] = Name,
            ["description"] = Description,
        };
        foreach (var (key, value) in Slots.ToJsonSchema())
        {
            schema[key] = value?.DeepClone();
        }

        return schema;
    }

    // Checks the slots against the schema, then the layout's rules across fields. Paths start with
    // the layout's name: "practice.do[2].lead".
    public void Validate(JsonElement slots, SlotValidation validation)
    {
        Slots.Validate(slots, Name, validation);
        if (validation.ErrorCount == 0)
        {
            _rules?.Invoke(slots, Name, validation);
        }
    }
}
