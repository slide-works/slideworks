namespace SlideWorks.WebApi.Features.Templates.Data;

// A pending upload of a template's file (SW-50 T1), keyed by the hash of its upload token.
public class TemplateUpload
{
    public required string TokenHash { get; set; }

    public required string TemplateId { get; set; }

    public TemplateAssetKind Kind { get; set; }

    // At most 120 characters, as TemplateAsset.FileName.
    public required string FileName { get; set; }

    public bool RightsConfirmed { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    // Null until the bytes have arrived.
    public DateTimeOffset? UsedAt { get; set; }
}
