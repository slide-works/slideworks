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
        var (definition, result) = await ReportAsync(images, deckId, template, layout, slots, surface);
        if (!result.IsValid)
        {
            throw new BadRequestException(result.Error);
        }

        return definition;
    }

    // The same check as ValidateAsync, without refusing: a dry run and a batch both need the fit report
    // of a slide that does not fit. An unknown layout is still a refusal, as add_slide's is.
    public static async Task<(LayoutDefinition Definition, SlideValidationResult Result)> ReportAsync(
        IDeckImages images, string deckId, ResolvedTemplate template, string layout, JsonElement slots, string? surface)
    {
        var definition = template.FindLayout(layout);
        if (definition is null)
        {
            // A name the template does not have. Core words the refusal for a name it does not know either, with the
            // names it knows; a built-in name gets no layout failure from Core, so it is worded here.
            // A built-in template keeps Core's list; an uploaded one names what its pinned version allows: the built-ins it
            // lists and its approved custom layouts. A lone surrogate in the slots is still Core's 'slots: ' refusal.
            var known = template.BuiltIn
                ? LayoutCatalogue.LayoutNames
                : LayoutCatalogue.LayoutNames.Where(template.Template.Allows).Concat(template.CustomLayouts.Select(c => c.Layout.Name)).ToArray();
            var builtIn = SlideValidator.Validate(layout, slots, known).Errors;
            throw new BadRequestException(builtIn.FirstOrDefault(error => error.StartsWith("layout: ", StringComparison.Ordinal))
                ?? builtIn.FirstOrDefault(error => error.StartsWith("slots: ", StringComparison.Ordinal))
                ?? $"layout: \"{layout}\" is not a layout of this deck's template; call list_layouts for the ones it has");
        }

        var result = SlideValidator.Validate(definition, slots, template.FitWidths, surface);
        if (!result.IsValid)
        {
            return (definition, result);
        }

        var missing = new List<string>();
        foreach (var imageId in result.ImageIds)
        {
            if (!await images.ExistsAsync(deckId, imageId))
            {
                missing.Add($"{ImagePath(layout, slots, imageId)}: unknown image {imageId}");
            }
        }

        return missing.Count == 0
            ? (definition, result)
            : (definition, new SlideValidationResult(missing, result.ImageIds) { Regions = result.Regions });
    }

    // The surface a slide is stored with. The audience feed has no template lookup: it reads the stored surface, else the
    // built-in catalogue's, so a slide on a custom layout that is dark by its own definition stores "dark" when none was
    // asked. Anything else stores what was asked, as before.
    public static string? SurfaceToStore(ResolvedTemplate template, string layout, string? asked)
        => asked is null && template.IsCustomLayout(layout) && template.FindLayout(layout)?.Surface == LayoutSurface.Dark
            ? SlideSurface.DARK
            : asked;

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
