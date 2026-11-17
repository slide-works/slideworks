namespace SlideWorks.Core.Qa;

// The one place a question's text and an asker's name are cleaned and checked (Q4): trimmed, with every run of whitespace
// (line breaks included) made one space. The errors name the field first, as POST /api/join/{code}/questions sends them.
// Lengths are in UTF-16 code units, as the phone's text box counts them.
public static class QuestionText
{
    public const int MAX_TEXT = 200;

    public const int MAX_NAME = 30;

    // The text as it is stored: trimmed and collapsed. Empty for null.
    public static string Text(string? text) => Collapse(text);

    // The name as it is stored: trimmed and collapsed, or null when nothing is left (the question is anonymous).
    public static string? Name(string? name) => Collapse(name) is { Length: > 0 } cleaned ? cleaned : null;

    // The first rule the cleaned text and name break, or null when they can be asked. NamesAllowed is the deck's names
    // setting.
    public static string? Check(string? text, string? name, bool namesAllowed)
    {
        var cleanedText = Text(text);
        if (cleanedText.Length == 0)
        {
            return "text: required";
        }

        if (cleanedText.Length > MAX_TEXT)
        {
            return $"text: at most {MAX_TEXT}";
        }

        var cleanedName = Name(name);
        if (cleanedName is null)
        {
            return null;
        }

        if (!namesAllowed)
        {
            return "name: not allowed";
        }

        return cleanedName.Length > MAX_NAME ? $"name: at most {MAX_NAME}" : null;
    }

    private static string Collapse(string? value) =>
        value is null ? "" : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
