namespace SlideWorks.WebApi.Features.Templates;

// The code of every template refusal (TemplateRefusedException.Code, and "error" on the wire). The text for each is in
// src/contracts/template-errors.json.
public static class TemplateErrorCodes
{
    public const string FONT_NOT_WOFF2 = "font_not_woff2";
    public const string FONT_WIDTH_OUT_OF_RANGE = "font_width_out_of_range";
    public const string SVG_SCRIPT = "svg_script";
    public const string SVG_EXTERNAL_REFERENCE = "svg_external_reference";
    public const string SVG_FOREIGN_OBJECT = "svg_foreign_object";
    public const string SVG_MALFORMED = "svg_malformed";
    public const string NOT_AN_IMAGE = "not_an_image";
    public const string PALETTE_CONTRAST = "palette_contrast";
    public const string LAYOUT_NOT_APPROVED = "layout_not_approved";
    public const string TOO_MANY_FONTS = "too_many_fonts";
    public const string TOO_MANY_LAYOUTS = "too_many_layouts";
    public const string TOO_MANY_EXTRAS = "too_many_extras";
    public const string TOO_MANY_TEMPLATES = "too_many_templates";
    public const string FILE_TOO_LARGE = "file_too_large";
    public const string RIGHTS_NOT_CONFIRMED = "rights_not_confirmed";
    public const string STALE_REVISION = "stale_revision";
    public const string MANIFEST_INVALID = "manifest_invalid";
    public const string ASSET_NOT_FOUND = "asset_not_found";
    public const string BUNDLE_INVALID = "bundle_invalid";
    public const string BUNDLE_TOO_LARGE = "bundle_too_large";
    public const string CUSTOM_LAYOUTS_UNAVAILABLE = "custom_layouts_unavailable";
    public const string RENDERER_UNAVAILABLE = "renderer_unavailable";
    public const string UPLOAD_USED = "upload_used";
    public const string NOT_PUBLISHED = "not_published";
    public const string TEMPLATE_STORAGE_FULL = "template_storage_full";
    public const string TOO_MANY_UPLOADS = "too_many_uploads";
}
