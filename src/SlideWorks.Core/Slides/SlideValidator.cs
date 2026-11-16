using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The one check every slide passes before it is stored (D3, K2): the layout is known, and the slots
// match its schema.
public static class SlideValidator
{
    public static SlideValidationResult Validate(string layout, JsonElement slots)
    {
        var validation = new SlotValidation();
        var definition = LayoutCatalogue.Find(layout);
        if (!IsValidUtf16(slots))
        {
            // Reading a lone surrogate (half an emoji, such as an escaped "\ud83d") as a .NET string throws, so the
            // schema walk would never return. Refuse the whole slide before it starts.
            validation.Fail("slots", "text is not valid UTF-16");
        }
        else if (definition is null)
        {
            validation.Fail("layout", $"unknown layout '{EchoText.Of(layout ?? "")}' (known: {string.Join(", ", LayoutCatalogue.LayoutNames)})");
        }
        else
        {
            definition.Validate(slots, validation);
        }

        return new SlideValidationResult(validation.Errors.ToArray(), validation.ErrorCount == 0 ? validation.ImageIds.ToArray() : []);
    }

    // True when every property name and string value in the slots decodes to valid UTF-16.
    private static bool IsValidUtf16(JsonElement element)
    {
        try
        {
            Decode(element);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void Decode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    _ = property.Name;
                    Decode(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Decode(item);
                }

                break;
            case JsonValueKind.String:
                _ = element.GetString();
                break;
        }
    }
}
