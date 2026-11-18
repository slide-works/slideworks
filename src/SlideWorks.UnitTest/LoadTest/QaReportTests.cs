using SlideWorks.LoadTest;

namespace SlideWorks.UnitTest.LoadTest;

[TestFixture]
public class QaReportTests
{
    [Test]
    public void From_WhenVotesAreSlowerThanTaps_ShouldGiveEachKindItsOwnP95BesideTheCombinedOne()
    {
        var options = LoadOptions.Parse(["--code", "1", "--scenario", "qa", "--guests", "1", "--no-results"]);
        var state = new RunState(0);
        var totals = new QaTotals();
        for (var i = 0; i < 3; i++)
        {
            state.RecordWrite(100);
            totals.RecordVote(100);
        }
        for (var i = 0; i < 10; i++)
        {
            state.RecordWrite(5);
            totals.RecordReaction(5);
        }

        var report = QaReport.From(options, state, totals);

        report.VotePutP95Ms.ShouldBe(100);
        report.TapP95Ms.ShouldBe(5);
        report.WriteP95Ms.ShouldNotBeNull();
        report.Table().ShouldContain("upvote PUT p95 100");
        report.Json().ShouldContain("\"votePutP95Ms\":100");
        report.Json().ShouldContain("\"tapP95Ms\":5");
    }
}
