namespace SlideWorks.WebApi.Tests.Guards;

// The production source files a guard test reads. They are found by walking up from the test binaries to the folder that
// holds the solution, like JsonShape.ScreensDirectory.
public static class ProductionSource
{
    public static string WebApiDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
            {
                return Path.Combine(directory.FullName, "SlideWorks.WebApi");
            }
        }
        throw new InvalidOperationException($"No SlideWorks.slnx above {AppContext.BaseDirectory}.");
    }

    public static IReadOnlyList<string> Files(string relativeDirectory, string pattern = "*.cs")
    {
        var directory = Path.Combine(WebApiDirectory(), relativeDirectory);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)).Order().ToList()
            : [];
    }
}
