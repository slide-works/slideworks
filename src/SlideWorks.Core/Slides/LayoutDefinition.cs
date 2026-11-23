using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// One layout: its frozen name, a one-line description for agents, the surface it is drawn on, its
// slot schema, what it reveals step by step, and whether it draws the tracker.
public sealed class LayoutDefinition
{
    public const string JSON_SCHEMA_DIALECT = "https://json-schema.org/draft/2020-12/schema";

    private readonly Action<JsonElement, string, SlotValidation>? _rules;

    public LayoutDefinition(
        string name,
        string description,
        LayoutSurface surface,
        IReadOnlyList<SlotField> fields,
        StepPlan? steps = null,
        Action<JsonElement, string, SlotValidation>? rules = null,
        bool tracker = true)
    {
        Name = name;
        Description = description;
        Surface = surface;
        Slots = new ObjectSlot(fields);
        Steps = steps;
        Tracker = tracker;
        _rules = rules;
    }

    // The constructor from before surfaces and step plans: a dark flag and one reveal list.
    public LayoutDefinition(
        string name,
        string description,
        bool dark,
        IReadOnlyList<SlotField> fields,
        string? stepsFrom = null,
        Action<JsonElement, string, SlotValidation>? rules = null)
        : this(name, description, dark ? LayoutSurface.Dark : LayoutSurface.Light, fields, stepsFrom is null ? null : StepPlan.List(stepsFrom), rules)
    {
    }

    public string Name { get; }

    public string Description { get; }

    public LayoutSurface Surface { get; }

    public bool Dark => Surface == LayoutSurface.Dark;

    public ObjectSlot Slots { get; }

    // What the layout reveals step by step (K16), or null when it has no steps of its own.
    public StepPlan? Steps { get; }

    // The first list field whose items appear one per step when reveal is "one-by-one" (K2), or
    // null when the plan has no list.
    public string? StepsFrom => Steps?.FirstList;

    // False when the layout leaves the deck's tracker out.
    public bool Tracker { get; }

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
            HeadingRules.Check(Name, slots, Name, validation);
            _rules?.Invoke(slots, Name, validation);
        }
    }
}
