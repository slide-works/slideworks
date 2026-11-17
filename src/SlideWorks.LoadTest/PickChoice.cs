namespace SlideWorks.LoadTest;

// What a voter picks: "random" is one option chosen uniformly, otherwise the 0-based positions listed in --pick.
public static class PickChoice
{
    public const string RANDOM = "random";

    public static void Validate(string pick)
    {
        if (pick != RANDOM && Parse(pick).Count == 0)
        {
            throw new ArgumentException("--pick must be random or 0-based positions such as 1 or 0,2.");
        }
    }

    public static IReadOnlyList<int> For(string pick, int optionCount)
        => pick == RANDOM ? [Dice.Next(optionCount)] : Parse(pick);

    // Empty when any part is not a position.
    private static List<int> Parse(string pick)
    {
        var picks = new List<int>();
        foreach (var part in pick.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var position) || position < 0)
            {
                return [];
            }
            picks.Add(position);
        }
        return picks;
    }
}
