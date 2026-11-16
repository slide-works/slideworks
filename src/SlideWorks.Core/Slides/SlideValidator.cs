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
        if (definition is null)
        {
            validation.Fail("layout", $"unknown layout '{layout}' (known: {string.Join(", ", LayoutCatalogue.LayoutNames)})");
        }
        else
        {
            definition.Validate(slots, validation);
        }

        return new SlideValidationResult(validation.Errors.ToArray(), validation.ErrorCount == 0 ? validation.ImageIds.ToArray() : []);
    }
}
