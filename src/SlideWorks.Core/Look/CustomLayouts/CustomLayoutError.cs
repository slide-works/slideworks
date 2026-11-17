namespace SlideWorks.Core.Look.CustomLayouts;

// One thing wrong with an uploaded layout. Path is layout.html:<line> or layout.css:<line> (1-based),
// slots.<slot path>, or layout for the whole upload. Message is the code's text; only a checked
// identifier may be appended to it, never a piece of the upload: Of(path, code, identifier) is the
// only way to append one.
public sealed record CustomLayoutError(string Path, string Code, string Message)
{
    public const string LAYOUT_PATH = "layout";

    // The longest identifier a message may carry.
    public const int MAX_IDENTIFIER = 64;

    // The error of a code with its own text and nothing appended.
    public static CustomLayoutError Of(string path, string code) => new(path, code, CustomLayoutErrorCodes.Text(code));

    // The error of a code with one identifier after its text, in brackets: a slot, tag, property or
    // class name the stage has already checked. Anything but lower-case letters, digits, hyphens and
    // underscores throws, so no piece of an upload can reach a message through here.
    public static CustomLayoutError Of(string path, string code, string identifier) =>
        IsIdentifier(identifier)
            ? new(path, code, $"{CustomLayoutErrorCodes.Text(code)} ({identifier})")
            : throw new ArgumentException("Only a checked identifier may follow an error's text.", nameof(identifier));

    private static bool IsIdentifier(string? text) =>
        text is { Length: > 0 and <= MAX_IDENTIFIER } && text.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_');
}
