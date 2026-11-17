using System.Text.RegularExpressions;

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

    // The files matching any of the glob patterns, relative to the WebApi project ("**" crosses folders, "*" does not).
    public static IReadOnlyList<string> Matching(IEnumerable<string> globs)
    {
        var root = WebApiDirectory();
        var matchers = globs.Select(glob => new Regex(
            "^" + Regex.Escape(glob).Replace(@"\*\*/", "(.*/)?").Replace(@"\*", "[^/]*") + "$", RegexOptions.CultureInvariant)).ToList();
        return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f =>
            {
                var relative = Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/');
                return matchers.Any(m => m.IsMatch(relative));
            })
            .Order()
            .ToList();
    }
}
