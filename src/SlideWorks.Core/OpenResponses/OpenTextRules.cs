namespace SlideWorks.Core.OpenResponses;

// The one check of what a guest types on a word-cloud or open-text slide (SW-8 D2, D3, D4). A failure is the error code
// that refuses the whole submit, before anything is written. Lengths are UTF-16 code units, as the phone's text box counts
// them, of the text after trimming and collapsing whitespace.
public static class OpenTextRules
{
    public const int MAX_WORDS = 3;

    public const int MAX_WORD_LENGTH = 25;

    public const int MAX_TEXT_LENGTH = 200;

    public const string REQUIRED = "required";

    public const string TOO_MANY_WORDS = "too_many_words";

    public const string WORD_TOO_LONG = "word_too_long";

    public const string TEXT_TOO_LONG = "text_too_long";

    public const string PROFANE = "profane";

    // The first rule a word-cloud submit breaks, or null when it is valid. Blank entries are dropped and entries with the
    // same key count once, so the limits apply to WordsOf(words).
    public static string? Check(IReadOnlyList<string> words)
    {
        var display = WordsOf(words);
        if (display.Count == 0)
        {
            return REQUIRED;
        }

        if (display.Count > MAX_WORDS)
        {
            return TOO_MANY_WORDS;
        }

        if (display.Any(word => word.Length > MAX_WORD_LENGTH))
        {
            return WORD_TOO_LONG;
        }

        return display.Any(IsProfane) ? PROFANE : null;
    }

    // The first rule an open-text answer breaks, or null when it is valid.
    public static string? Check(string text)
    {
        var answer = Collapse(text);
        if (answer.Length == 0)
        {
            return REQUIRED;
        }

        if (answer.Length > MAX_TEXT_LENGTH)
        {
            return TEXT_TOO_LONG;
        }

        return IsProfane(answer) ? PROFANE : null;
    }

    // The words a submit stores, in the order sent: each collapsed, blanks dropped, and one per key with the first casing.
    public static IReadOnlyList<string> WordsOf(IEnumerable<string> words) =>
        [.. words.Select(Collapse).Where(word => word.Length > 0).DistinctBy(KeyOf, StringComparer.Ordinal)];

    // A word's identity (D2): trimmed, whitespace collapsed and case-folded. No stemming.
    public static string KeyOf(string word) => Collapse(word).ToLowerInvariant();

    // The text trimmed, with each run of whitespace inside it made one space.
    public static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Whether any whole word of the collapsed text, without the punctuation around it, is on the profanity list.
    private static bool IsProfane(string text) =>
        text.Split(' ').Any(word => ProfanityList.Contains(KeyOf(word.Trim(Punctuation(word)))));

    private static char[] Punctuation(string word) => [.. word.Where(c => char.IsPunctuation(c) || char.IsSymbol(c)).Distinct()];
}
