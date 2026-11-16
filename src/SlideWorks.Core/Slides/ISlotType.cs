using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// One node of a layout's slot schema. The model is deliberately small and our own (no JSON Schema
// library): it validates a slot value, naming the path that broke, and emits standard JSON Schema
// for agents.
public interface ISlotType
{
    // What a value of this type is, as an error message says it: "expected text".
    string Expected { get; }

    // A short kind for the generated docs: "text", "list of object".
    string Kind { get; }

    JsonObject ToJsonSchema();

    void Validate(JsonElement value, string path, SlotValidation validation);
}
