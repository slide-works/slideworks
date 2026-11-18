using System.Text.Json.Serialization;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// One template in list_templates (L7): what it is for, the layouts it allows, its chrome defaults, the outline and house
// style an agent writes the deck to, its type, and the palette it locks, if any. is_default marks the template a new deck
// gets when none is given, so no tool description needs to name it. Ref is what to pass as a template argument (a built-in's
// name or a tpl_ id), Source says where it comes from (builtin, yours or shared), Version is its latest published version
// (null on a built-in) and Owner a shared template's owner by display name (null on a built-in and on yours).
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
    [property: JsonPropertyName("locked_palette")] string? LockedPalette,
    string Ref,
    string Source,
    int? Version,
    string? Owner)
{
    public const string BUILT_IN = "builtin";
    public const string YOURS = "yours";
    public const string SHARED = "shared";

    public static ListTemplatesEntry From(ResolvedTemplate resolved, Guid? me)
        => From(resolved.Template with { Name = resolved.Ref }, resolved, SourceOf(resolved, me));

    private static string SourceOf(ResolvedTemplate resolved, Guid? me)
    {
        if (resolved.BuiltIn)
        {
            return BUILT_IN;
        }
        return resolved.OwnerId is { } owner && owner == me ? YOURS : SHARED;
    }

    private static ListTemplatesEntry From(Template template, ResolvedTemplate resolved, string source)
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
            template.LockPalette ? template.Palette : null,
            resolved.Ref,
            source,
            resolved.BuiltIn ? null : resolved.Version,
            source == SHARED ? resolved.OwnerName : null);
}
