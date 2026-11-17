namespace SlideWorks.WebApi.Features.Templates.Data;

// A file of a template (SW-50 T1): a font, a logo, a piece of art or a custom layout, keyed by the template and the id.
public class TemplateAsset
{
    public const int ID_LENGTH = 32;

    public const int FILE_NAME_MAX = 120;

    // The first 32 hex characters of the SHA-256.
    public required string Id { get; set; }

    public required string TemplateId { get; set; }

    public TemplateAssetKind Kind { get; set; }

    // The name it was uploaded with, at most 120 characters; for display and bundle export only.
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required string Sha256 { get; set; }

    public long Bytes { get; set; }

    public string? MetaJson { get; set; }

    public bool RightsConfirmed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
