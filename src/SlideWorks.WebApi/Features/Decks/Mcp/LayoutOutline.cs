using System.Text.Json.Serialization;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout in a compact list_layouts: the slots, their limits and how many items fit, without the JSON Schema.
public sealed record LayoutOutline(
    string Name,
    string Description,
    bool Dark,
    string? StepsFrom,
    string Surface,
    string? Steps,
    bool Tracker,
    IReadOnlyList<SlotCapacity> Capacity,
    IReadOnlyList<SlotOutline> Slots)
{
    public static LayoutOutline From(LayoutDefinition layout)
        => new(
            layout.Name,
            layout.Description,
            layout.Dark,
            layout.StepsFrom,
            SurfaceName(layout.Surface),
            layout.Steps?.Phrase,
            layout.Tracker,
            LayoutCapacity.Of(layout),
            layout.Slots.Fields.Select(SlotOutline.Of).ToList());

    private static string SurfaceName(LayoutSurface surface) => surface switch
    {
        LayoutSurface.Light => "light",
        LayoutSurface.Dark => "dark",
        LayoutSurface.Either => "either",
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };
}

// One slot in that outline, and one level of the fields inside a list or object.
public sealed record SlotOutline(
    string Name,
    bool Required,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Min,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Max,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxLength,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SlotOutline>? Fields)
{
    public static SlotOutline Of(SlotField field)
    {
        int? min = null;
        int? max = null;
        int? maxLength = null;
        IReadOnlyList<SlotOutline>? fields = null;
        switch (field.Type)
        {
            case TextSlot text:
                maxLength = text.MaxLength;
                break;
            case NumberSlot number:
                min = Integral(number.Minimum);
                max = Integral(number.Maximum);
                break;
            case ListSlot list:
                min = list.MinItems;
                max = list.MaxItems;
                fields = FieldsOf(list.Item);
                break;
            default:
                fields = FieldsOf(field.Type);
                break;
        }

        return new SlotOutline(field.Name, field.Required, field.Type.Kind, field.Description, min, max, maxLength, fields);
    }

    private static int? Integral(double value)
        => double.IsInteger(value) && value is >= int.MinValue and <= int.MaxValue ? (int)value : null;

    private static IReadOnlyList<SlotOutline>? FieldsOf(ISlotType type) => type switch
    {
        ObjectSlot obj => obj.Fields.Select(Of).ToList(),
        BlockSlot block when block.Block.Type is ObjectSlot obj => obj.Fields.Select(Of).ToList(),
        TextOrObjectSlot either => FieldsOf(either.Richer),
        _ => null,
    };
}
