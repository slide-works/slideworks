using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The numbering rule of an open-text wall, shared by the guest and presenter views and the print view: a guest's one answer
// (position 0) is numbered 1-based in arrival order, which an update of the text leaves alone, so the number is stable.
// Hidden answers keep their number, so the others show with gaps.
public static class OpenTextWall
{
    public static IQueryable<OpenEntry> InArrivalOrder(IQueryable<OpenEntry> entries, Guid roundId)
        => entries.Where(e => e.RoundId == roundId && e.Position == 0).OrderBy(e => e.Arrival);

    public static IReadOnlyList<WallAnswer> Number(IEnumerable<(string Text, bool Hidden)> answers)
        => [.. answers.Select((answer, index) => new WallAnswer(index + 1, answer.Text, answer.Hidden))];
}
