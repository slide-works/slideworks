using System.Text.Json.Serialization;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slot in a compact list_layouts: its name (left out on a list's item), its kind, whether it is required, and its
// limit (a text's length or a list's item count, else null). Values is only on an enum, Item only on a list, Slots on an
// object and on the object form of a text-or-object, Block only when the kind is block. A youtube slot (the video
// layout's url) is a link that must be one YouTube video, so it keeps a kind of its own.
public sealed record CompactSlot(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    string Kind,
    bool Required,
    int? Max,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Values = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CompactSlot? Item = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CompactSlot>? Slots = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Block = null)
{
    public static CompactSlot From(SlotField field) => From(field.Name, field.Type, field.Required);

    public static CompactSlot From(string? name, ISlotType type, bool required) => type switch
    {
        TextSlot text => new(name, "text", required, text.MaxLength),
        EnumSlot choice => new(name, "enum", required, null, Values: choice.Values),
        ListSlot list => new(name, "list", required, list.MaxItems, Item: From(null, list.Item, true)),
        ImageSlot => new(name, "image", required, null),
        LinkSlot link => new(name, "link", required, link.MaxLength),
        YouTubeSlot => new(name, "youtube", required, LinkSlot.MAX_LENGTH),
        IconSlot => new(name, "icon", required, null),
        NumberSlot => new(name, "number", required, null),
        TextOrObjectSlot either => new(name, "text-or-object", required, either.Text.MaxLength, Slots: FieldsOf(either.Richer.Fields)),
        BlockSlot block => new(name, "block", required, null, Block: block.Block.Name),
        EmphasisSlot => new(name, "block", required, null, Block: "emphasis"),
        ObjectSlot obj => new(name, "object", required, null, Slots: FieldsOf(obj.Fields)),
        _ => throw new InvalidOperationException($"No compact kind for slot type {type.GetType().Name}"),
    };

    public static IReadOnlyList<CompactSlot> FieldsOf(IEnumerable<SlotField> fields) => [.. fields.Select(From)];
}
