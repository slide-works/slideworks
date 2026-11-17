namespace SlideWorks.WebApi.Tests.Support;

// The two font files committed for tests (Support/Fonts, see its README), and bytes that are not a font. The only place
// a test gets a font from.
public static class FontFixtures
{
    // Inter 400, the built-in body font. An absolute path, for a file input.
    public static string NormalPath => Path.Combine(FontsDirectory(), "normal.woff2");

    // Inter 900, a little wider than the normal file. An absolute path, for a file input.
    public static string WidePath => Path.Combine(FontsDirectory(), "wide.woff2");

    public static byte[] Normal() => File.ReadAllBytes(NormalPath);

    public static byte[] Wide() => File.ReadAllBytes(WidePath);

    // A TrueType header (00 01 00 00) followed by zeros: the right first bytes for a font, and nothing a font check accepts.
    public static byte[] NotAFont()
    {
        var bytes = new byte[64];
        bytes[1] = 1;
        return bytes;
    }

    // SlideWorks.WebApi.Tests/Support/Fonts, found by walking up from the test binaries to the folder that holds the
    // solution, so the acceptance project reads the same files.
    private static string FontsDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
            {
                return Path.Combine(directory.FullName, "SlideWorks.WebApi.Tests", "Support", "Fonts");
            }
        }
        throw new InvalidOperationException($"No SlideWorks.slnx above {AppContext.BaseDirectory}.");
    }
}
