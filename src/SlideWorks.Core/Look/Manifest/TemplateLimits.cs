namespace SlideWorks.Core.Look.Manifest;

// The limits of a custom template (T3). template-errors.json names each one in its text.
public static class TemplateLimits
{
    public const int MAX_FONT_FILES = 8;
    public const int MAX_FONT_BYTES = 2 * 1024 * 1024;

    // A logo or a title art file.
    public const int MAX_IMAGE_BYTES = 10 * 1024 * 1024;

    public const int MAX_CUSTOM_LAYOUTS = 12;

    // A custom layout's upload document, and each of its three parts: the markup, the CSS and the slots.
    public const int MAX_LAYOUT_BYTES = 800_000;
    public const int MAX_LAYOUT_PART_BYTES = 262_144;

    public const int MAX_EXTRAS = 4;
    public const int MAX_ORNAMENT_DOTS = 6;

    // Archived templates are not counted.
    public const int MAX_TEMPLATES_PER_PERSON = 50;

    // A bundle once unpacked, and the number of files in it.
    public const int MAX_BUNDLE_BYTES = 60 * 1024 * 1024;
    public const int MAX_BUNDLE_ENTRIES = 64;

    // Everything one template has ever stored. A stored file is never removed, because a published version's files must
    // stay, so the room is four bundles' worth: an owner can replace fonts and logos across versions before it runs out.
    public const int MAX_STORED_ASSETS = 256;
    public const long MAX_STORED_BYTES = 240_000_000;
}
