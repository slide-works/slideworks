using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Slides;

// The checks add_slide (SlideCommands) and the edit tools (SlideEdits) share. Each refusal is a BadRequestException
// whose message names the field, so an agent gets the same text whichever tool it called.
public static class SlideChecks
{
    public const int NOTES_MAX_LENGTH = 4000;

    // The surface as a tool got it, as the word the validator checks. A JSON value that is not text goes in as its raw
    // JSON, which is never a surface, so the validator refuses it with the text it has for any other wrong word.
    public static string? SurfaceWord(JsonElement? given) => given?.ValueKind switch
    {
        null or JsonValueKind.Undefined or JsonValueKind.Null => null,
        JsonValueKind.String => given.Value.GetString(),
        _ => given.Value.GetRawText(),
    };

    // The check for a slide of a deck on this template: the layout is the template's, the text is wrapped with the
    // widths of the template's fonts, and surface is the one the slide will have ("light", "dark" or null for the
    // layout's own), then the images. Returns the layout's definition, which the caller counts the steps with.
    public static async Task<LayoutDefinition> ValidateAsync(IDeckImages images, string deckId, ResolvedTemplate template, string layout, JsonElement slots, string? surface)
    {
        var definition = template.FindLayout(layout);
        if (definition is null)
        {
            // A name the template does not have. Core words the refusal for a name it does not know either, with the
            // names it knows; a built-in name gets no layout failure from Core, so it is worded here.
            var builtIn = SlideValidator.Validate(layout, slots).Errors;
            throw new BadRequestException(builtIn.FirstOrDefault(error => error.StartsWith("layout: ", StringComparison.Ordinal))
                ?? builtIn.FirstOrDefault(error => error.StartsWith("slots: ", StringComparison.Ordinal))
                ?? $"layout: \"{layout}\" is not a layout of this deck's template; call list_layouts for the ones it has");
        }
        await ValidateAsync(images, deckId, layout, slots, SlideValidator.Validate(definition, slots, template.FitWidths, surface));
        return definition;
    }

    private static async Task ValidateAsync(IDeckImages images, string deckId, string layout, JsonElement slots, SlideValidationResult validation)
    {
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error);
        }
        foreach (var imageId in validation.ImageIds)
        {
            if (!await images.ExistsAsync(deckId, imageId))
            {
                throw new BadRequestException($"{ImagePath(layout, slots, imageId)}: unknown image {imageId}");
            }
        }
    }

    // A 1-based part, or null for none (add_slide) or to keep the slide's own (update_slide).
    public static void CheckPart(int? part, int parts)
    {
        if (part == null || (part >= 1 && part <= parts))
        {
            return;
        }
        throw new BadRequestException(parts == 0 ? "part: the deck has no parts" : $"part: must be between 1 and {parts}");
    }

    // The path of the image slot that holds imageId, in the validator's form: "image.image", "two-column.left.image".
    private static string ImagePath(string layout, JsonElement slots, string imageId)
        => FindImage(slots, layout, imageId) ?? $"{layout}.image";

    private static string? FindImage(JsonElement element, string path, string imageId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Select((item, index) => FindImage(item, $"{path}[{index}]", imageId)).FirstOrDefault(found => found != null);
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("image", out var id) && id.ValueKind == JsonValueKind.String && id.ValueEquals(imageId)
                && property.Value.TryGetProperty("alt", out _))
            {
                return $"{path}.{property.Name}";
            }
            var found = FindImage(property.Value, $"{path}.{property.Name}", imageId);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
