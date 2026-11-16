using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

public sealed class ListSlot(ISlotType item, int minItems, int maxItems) : ISlotType
{
    public ISlotType Item { get; } = item;

    public int MinItems { get; } = minItems;

    public int MaxItems { get; } = maxItems;

    public string Expected => "a list";

    public string Kind => "list of " + Item.Kind;

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "array",
        ["minItems"] = MinItems,
        ["maxItems"] = MaxItems,
        ["items"] = Item.ToJsonSchema(),
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var count = value.GetArrayLength();
        if (count < MinItems)
        {
            validation.Fail(path, $"at least {Items(MinItems)}");
        }
        else if (count > MaxItems)
        {
            // Don't walk an oversized list: its items can't be stored anyway, and checking them would
            // let the size of the request set the cost of the check.
            validation.Fail(path, $"at most {Items(MaxItems)}");
            return;
        }

        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            Item.Validate(element, $"{path}[{index}]", validation);
            index++;
        }
    }

    private static string Items(int count) => count == 1 ? "1 item" : $"{count} items";
}
