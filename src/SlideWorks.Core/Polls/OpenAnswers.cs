namespace SlideWorks.Core.Polls;

// Free answers on a text or word-cloud question. A reply is the guest's own words. A cloud counts the words in every
// reply, so the room sees the words and never who wrote them.
public static class OpenAnswers
{
    public const int MAX_TEXT = 200;

    private const int MAX_WORDS = 40;
    private const int MIN_WORD = 2;

    // What is wrong with the text as an open answer, or null when it is one.
    public static string? Problem(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "text: required";
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MAX_TEXT)
        {
            return $"text: at most {MAX_TEXT} characters (got {trimmed.Length})";
        }

        return trimmed.Any(char.IsControl) ? "text: must not contain control characters" : null;
    }

    public static string Clean(string text) => text.Trim();

    // The replies in the order they were given, without empty ones.
    public static IReadOnlyList<string> Replies(IEnumerable<string> texts)
        => texts.Select(text => text.Trim()).Where(text => text.Length > 0).ToList();

    // The words used most, then the rest alphabetically, at most MAX_WORDS. A word is a run of letters or digits.
    public static IReadOnlyList<PollWord> Words(IEnumerable<string> texts)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var text in texts)
        {
            foreach (var word in WordsIn(text))
            {
                counts[word] = counts.GetValueOrDefault(word) + 1;
            }
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(MAX_WORDS)
            .Select(pair => new PollWord(pair.Key, pair.Value))
            .ToList();
    }

    private static IEnumerable<string> WordsIn(string text)
    {
        var word = new List<char>();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Add(char.ToLowerInvariant(character));
                continue;
            }

            if (word.Count >= MIN_WORD)
            {
                yield return new string(word.ToArray());
            }

            word.Clear();
        }

        if (word.Count >= MIN_WORD)
        {
            yield return new string(word.ToArray());
        }
    }
}
