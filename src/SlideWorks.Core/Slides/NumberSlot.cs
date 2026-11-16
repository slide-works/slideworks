using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

public sealed class NumberSlot(double minimum, double maximum, bool wholeNumber) : ISlotType
{
    public double Minimum { get; } = minimum;

    public double Maximum { get; } = maximum;

    public bool WholeNumber { get; } = wholeNumber;

    public string Expected => WholeNumber ? "a whole number" : "a number";

    public string Kind => WholeNumber ? "whole number" : "number";

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = WholeNumber ? "integer" : "number",
        ["minimum"] = Minimum,
        ["maximum"] = Maximum,
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number)
            || (WholeNumber && !double.IsInteger(number)))
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        if (number < Minimum)
        {
            validation.Fail(path, $"at least {Format(Minimum)}");
        }
        else if (number > Maximum)
        {
            validation.Fail(path, $"at most {Format(Maximum)}");
        }
    }

    private static string Format(double number) => number.ToString(CultureInfo.InvariantCulture);
}
