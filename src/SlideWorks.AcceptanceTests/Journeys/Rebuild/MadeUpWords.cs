namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// Made-up text about the made-up company Northwind, of an exact length: a slot's documented maximum. Whole words
// while they fit, then one filler word (or one more letter on the last word) so the length is exact.
public static class MadeUpWords
{
    private static readonly string[] WORDS =
    [
        "harbour", "ledger", "crate", "pilot", "anchor", "beacon", "quarry", "tidal", "meadow", "copper", "lantern",
        "compass", "ferry", "granary", "keel", "lighthouse", "mooring", "orchard", "pier", "rigging", "sail", "wharf",
    ];

    private const string FILLER = "northwindharbourledgercratepilot";

    // Text of exactly {length} characters. {salt} moves where the word list starts, so two texts differ.
    public static string Of(int length, int salt = 0)
    {
        var text = new System.Text.StringBuilder();
        var next = salt;
        while (true)
        {
            var word = WORDS[Math.Abs(next++) % WORDS.Length];
            var needed = text.Length == 0 ? word.Length : word.Length + 1;
            var rest = length - text.Length;
            if (needed > rest)
            {
                break;
            }
            text.Append(text.Length == 0 ? word : " " + word);
        }
        var remaining = length - text.Length;
        if (remaining >= 2)
        {
            text.Append(' ').Append(FILLER.AsSpan(0, remaining - 1));
        }
        else if (remaining == 1)
        {
            text.Append('s');
        }
        var result = text.ToString();
        return char.ToUpperInvariant(result[0]) + result[1..];
    }
}
