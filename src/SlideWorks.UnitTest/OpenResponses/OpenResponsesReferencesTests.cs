using System.Text.RegularExpressions;
using SlideWorks.UnitTest.Slides;

namespace SlideWorks.UnitTest.OpenResponses;

// SW-8 D1: open responses sit beside polls and Q&A and never reach into them. The source under the WebApi's
// Features/OpenResponses names neither area; a missing folder has nothing in it to check.
[TestFixture]
public partial class OpenResponsesReferencesTests
{
    [Test]
    public void References_WhenOpenResponsesIsAbsent_ShouldPass()
    {
        var folder = Path.GetFullPath(ContractFiles.PathOf(Path.Combine("..", "SlideWorks.WebApi", "Features", "OpenResponses")));
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories) : [];

        files.Where(file => PollsOrQa().IsMatch(File.ReadAllText(file))).Select(file => Path.GetRelativePath(folder, file)).ShouldBeEmpty();
    }

    [GeneratedRegex(@"\bFeatures\.(Polls|Qa)\b")]
    private static partial Regex PollsOrQa();
}
