using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Slides;

// The checks add_slide (SlideCommands) and the edit tools (SlideEdits) share. Each refusal is a BadRequestException
// whose message names the field, so an agent gets the same text whichever tool it called.
public static class SlideChecks
{
    public const int NOTES_MAX_LENGTH = 4000;

    // The slots argument, which the tools take as optional so that leaving it out gets this message rather than the MCP
    // SDK's generic binding error.
    public static JsonElement RequireSlots(JsonElement? slots)
        => slots is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } given
            ? given
            : throw new BadRequestException("slots: required");

    // The layout's schema, then every image slot's id against the deck's completed uploads.
    public static async Task ValidateAsync(IDeckImages images, string deckId, string layout, JsonElement slots)
    {
        var validation = SlideValidator.Validate(layout, slots);
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
