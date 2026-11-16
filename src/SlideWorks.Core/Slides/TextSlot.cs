using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// Plain text (D12): never Markdown or HTML. Line breaks only where the layout allows them, and no
// control characters.
public sealed class TextSlot(int maxLength, bool lineBreaks = false) : ISlotType
{
    // Combining marks count as part of one character, so the raw UTF-16 length gets a cap as well.
    private const int UTF16_UNITS_PER_CHARACTER = 4;

    public int MaxLength { get; } = maxLength;

    public bool LineBreaks { get; } = lineBreaks;

    public string Expected => "text";

    public string Kind => "text";

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["minLength"] = 1,
        ["maxLength"] = MaxLength,
        ["pattern"] = LineBreaks ? "\\S" : "^[^\\r\\n\\u2028\\u2029]*\\S[^\\r\\n\\u2028\\u2029]*$",
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var text = value.GetString() ?? "";
        if (text.All(c => char.IsWhiteSpace(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format))
        {
            validation.Fail(path, "must not be empty");
            return;
        }

        if (text.Any(c => c is '\r' or '\u2028' or '\u2029' || (c == '\n' && !LineBreaks)))
        {
            validation.Fail(path, LineBreaks ? "use \\n for a line break" : "line breaks are not allowed here");
            return;
        }

        if (text.Any(c => c != '\n' && char.IsControl(c)))
        {
            validation.Fail(path, "control characters are not allowed");
            return;
        }

        var length = new StringInfo(text).LengthInTextElements;
        if (length > MaxLength || text.Length > MaxLength * UTF16_UNITS_PER_CHARACTER)
        {
            validation.Fail(path, $"at most {MaxLength} characters (got {Math.Max(length, text.Length / UTF16_UNITS_PER_CHARACTER)})");
        }
    }

    // How long the text is in characters, as the limits count them.
    public static int Length(string text) => new StringInfo(text).LengthInTextElements;
}
