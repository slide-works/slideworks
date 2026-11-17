using System.Text.Json.Serialization;
using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// One template in list_templates (L7): what it is for, the layouts it allows, its chrome defaults, the outline and house
// style an agent writes the deck to, its type, and the palette it locks, if any. is_default marks the template a new deck
// gets when none is given, so no tool description needs to name it.
public sealed record ListTemplatesEntry(
    string Name,
    string Label,
    string Purpose,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    IReadOnlyList<string> Layouts,
    TemplateChrome Chrome,
    IReadOnlyList<string> Outline,
    [property: JsonPropertyName("house_style")] IReadOnlyList<string> HouseStyle,
    TemplateType Type,
    [property: JsonPropertyName("locked_palette")] string? LockedPalette)
{
    public static ListTemplatesEntry From(Template template)
        => new(
            template.Name,
            template.Label,
            template.Purpose,
            template.IsDefault,
            template.Layouts,
            template.Chrome,
            template.Outline,
            template.HouseStyle,
            template.Type,
            template.LockPalette ? template.Palette : null);
}
