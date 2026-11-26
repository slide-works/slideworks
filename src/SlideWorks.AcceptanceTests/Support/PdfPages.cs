using System.Text;
using System.Text.RegularExpressions;

namespace SlideWorks.AcceptanceTests.Support;

// Counts the pages of a PDF without a PDF library: the page objects are the "/Type /Page" entries that are not the
// "/Type /Pages" tree nodes. Chromium writes the dictionary keys uncompressed, so a Latin-1 read of the bytes is enough.
public static partial class PdfPages
{
    public static bool IsPdf(byte[] pdf) => pdf.Length > 5 && Encoding.Latin1.GetString(pdf, 0, 5) == "%PDF-";

    public static int Count(byte[] pdf) => PageObject().Matches(Encoding.Latin1.GetString(pdf)).Count;

    [GeneratedRegex(@"/Type\s*/Page(?![A-Za-z])")]
    private static partial Regex PageObject();
}
