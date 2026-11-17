using System.Text.RegularExpressions;

namespace SlideWorks.WebApi.Tests.Guards;

// SlideEdits.EditAsync is public so the poll tools can write inside the house edit transaction, but the rule that a poll slide
// with a round open can't be deleted or given another layout sits in SlideEdits.DeleteAsync and UpdateAsync, not in it. So
// the files that may call it are listed here: a new caller has to be added on purpose, after checking that it neither deletes
// a slide nor changes its layout.
[TestFixture]
public class SlideEditsCallerGuardTests
{
    private static readonly Regex _calls = new(@"\.EditAsync\s*\(", RegexOptions.CultureInvariant);

    private static readonly string[] _mayCall = ["PollSettingsCommands.cs"];

    [Test]
    public void EditAsync_ShouldBeCalledOnlyByTheFilesThatAreKnownNotToDeleteOrRetypeASlide()
    {
        var callers = ProductionSource.Files("Features")
            .Where(file => _calls.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .ToList();

        callers.ShouldBe(_mayCall);
    }
}
