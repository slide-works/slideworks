using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls;

// The rules a guest's picks must pass (V5), checked in this order once the answer is known not to be stale: required,
// unknown option, duplicate, pick one, at most N. Picks are 0-based option positions; an unknown one is named as it was
// sent. Pure: the poll's settings and its option count come from the caller.
public static class AnswerRules
{
    public const string DUPLICATE = "picks: duplicate";
    public const string PICK_ONE = "picks: pick one";

    // The first rule the picks break, or null when they pass.
    public static string? Check(IReadOnlyList<int> picks, PollSettings settings, int optionCount)
    {
        if (picks.Count == 0)
        {
            return AnswerBody.PICKS_REQUIRED;
        }
        var unknown = picks.Where(pick => pick < 0 || pick >= optionCount).Take(1).ToList();
        if (unknown.Count > 0)
        {
            return $"picks: unknown option {unknown[0]}";
        }
        if (picks.Distinct().Count() != picks.Count)
        {
            return DUPLICATE;
        }
        if (settings.Pick == PollPick.One && picks.Count > 1)
        {
            return PICK_ONE;
        }
        if (settings.Pick == PollPick.UpTo && settings.Max is { } max && picks.Count > max)
        {
            return $"picks: at most {max}";
        }
        return null;
    }
}
