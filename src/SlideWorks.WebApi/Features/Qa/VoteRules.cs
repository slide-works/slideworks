using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Data;

namespace SlideWorks.WebApi.Features.Qa;

// The rules a guest's upvotes must pass and what is stored from them (Q4, D21), once the vote is known not to be stale.
// Picks on the wire are 1-based question numbers; stored picks are 0-based positions. Pure: the round's questions and the
// guest's stored picks come from the caller.
public static class VoteRules
{
    public const string DUPLICATE = "picks: duplicate";

    // The first rule the picks break, or null when they pass: a number the round has no question at (below 1 or past the
    // last, named as it was sent), then a number sent twice.
    public static string? Check(IReadOnlyList<int> numbers, IReadOnlyList<Question> questions)
    {
        var known = questions.Select(q => q.Position + 1).ToHashSet();
        var unknown = numbers.Where(number => !known.Contains(number)).Take(1).ToList();
        if (unknown.Count > 0)
        {
            return $"picks: unknown question {unknown[0]}";
        }
        return numbers.Distinct().Count() != numbers.Count ? DUPLICATE : null;
    }

    // The positions to store, ascending: the guest's stored picks on questions that can't take a vote now (waiting, hidden or
    // answered), kept whatever was asked, plus the asked numbers on questions that can (open and not hidden).
    public static IReadOnlyList<int> Stored(IReadOnlyList<int> storedPositions, IReadOnlyList<int> numbers, IReadOnlyList<Question> questions)
    {
        var votable = questions.Where(Votable).Select(q => q.Position).ToHashSet();
        return
        [
            .. storedPositions.Where(position => !votable.Contains(position))
                .Concat(numbers.Select(number => number - 1).Where(votable.Contains))
                .Order(),
        ];
    }

    private static bool Votable(Question question) => question.State == QuestionState.Open && question.HiddenAt is null;
}
