namespace SlideWorks.Core.Look.CustomLayouts;

// One thing wrong with an uploaded layout. Path is layout.html:<line> or layout.css:<line> (1-based),
// slots.<slot path>, or layout for the whole upload. Message is the code's text; only a checked
// identifier may be appended to it, never a piece of the upload.
public sealed record CustomLayoutError(string Path, string Code, string Message)
{
    public const string LAYOUT_PATH = "layout";

    // The error of a code with its own text and nothing appended.
    public static CustomLayoutError Of(string path, string code) => new(path, code, CustomLayoutErrorCodes.Text(code));
}
