using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The one check every slide passes before it is stored (D3, K2): the layout is known, and the slots
// match its schema.
public static class SlideValidator
{
    private const string NOT_UTF16 = "text is not valid UTF-16";

    public static SlideValidationResult Validate(string layout, JsonElement slots)
    {
        var definition = LayoutCatalogue.Find(layout);
        if (definition is not null)
        {
            return Validate(definition, slots, FitWidths.Default);
        }

        var validation = new SlotValidation();
        if (!IsValidUtf16(slots))
        {
            validation.Fail("slots", NOT_UTF16);
        }
        else
        {
            validation.Fail("layout", $"unknown layout '{EchoText.Of(layout ?? "")}' (known: {string.Join(", ", LayoutCatalogue.LayoutNames)})");
        }

        return Result(validation);
    }

    // The same check for a layout already found, with the glyph widths of the deck's fonts. The
    // surface is the slide's own: "light", "dark" or null. One the layout cannot take is refused.
    public static SlideValidationResult Validate(LayoutDefinition definition, JsonElement slots, FitWidths widths, string? surface = null)
    {
        var validation = new SlotValidation { Widths = widths };
        SlideSurface.Resolve(definition, surface, validation);
        if (!IsValidUtf16(slots))
        {
            // Reading a lone surrogate (half an emoji, such as an escaped "\ud83d") as a .NET string throws, so the
            // schema walk would never return. Refuse the whole slide before it starts.
            validation.Fail("slots", NOT_UTF16);
        }
        else
        {
            definition.Validate(slots, validation);
        }

        return Result(validation);
    }

    private static SlideValidationResult Result(SlotValidation validation) =>
        new(validation.Errors.ToArray(), validation.ErrorCount == 0 ? validation.ImageIds.ToArray() : [])
        {
            Regions = validation.Regions,
        };

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
