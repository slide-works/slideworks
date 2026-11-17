namespace SlideWorks.WebApi.Features.Templates.Data;

// One version of a template (SW-50 T1), keyed by the template and the version number. Version 0 is the draft; published
// versions count from 1, and a published version never changes.
public class TemplateVersion
{
    public const int DRAFT = 0;

    public required string TemplateId { get; set; }

    public int Version { get; set; }

    public required string ManifestJson { get; set; }

    // The widths the font check measured, as JSON, or null when there are none.
    public string? FitWidthsJson { get; set; }

    // Null for the draft.
    public DateTimeOffset? PublishedAt { get; set; }
}
