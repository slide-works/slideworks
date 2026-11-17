using System.Text.Json;

namespace SlideWorks.Core.Look.Manifest;

// Reads typed values out of a manifest's JSON and collects what is wrong, each with its path. A value that is wrong
// reads as an empty one, so ManifestJson can build the whole manifest and then look at Errors once.
internal sealed class ManifestReader
{
    public const int MAX_ERRORS = 20;

    private readonly List<ManifestError> _errors = [];

    public IReadOnlyList<ManifestError> Errors => _errors;

    public void Add(string path, string message)
    {
        if (_errors.Count < MAX_ERRORS)
        {
            _errors.Add(new ManifestError(path, message));
        }
    }

    public static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

    // True when the element is an object. Every field outside `known` is refused.
    public bool IsObject(JsonElement element, string path, IReadOnlyCollection<string> known)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(path, "must be an object");
            return false;
        }

        foreach (var property in element.EnumerateObject().Where(property => !known.Contains(property.Name)))
        {
            Add(Join(path, property.Name), "is not a field of a template manifest");
        }

        return true;
    }

    // The field's element, or null when it is absent; a required field that is absent is an error.
    public JsonElement? Field(JsonElement parent, string path, string key, bool required = true)
    {
        if (parent.TryGetProperty(key, out var value))
        {
            return value;
        }

        if (required)
        {
            Add(Join(path, key), "is required");
        }

        return null;
    }

    public string Text(JsonElement parent, string path, string key, int minLength = 0, int? maxLength = null) =>
        OptionalText(parent, path, key, minLength, maxLength, required: true) ?? "";

    public string? OptionalText(JsonElement parent, string path, string key, int minLength = 0, int? maxLength = null, bool required = false)
    {
        if (Field(parent, path, key, required) is not { } value)
        {
            return null;
        }

        return TextOf(value, Join(path, key), minLength, maxLength);
    }

    public string OneOf(JsonElement parent, string path, string key, IReadOnlyList<string> allowed)
    {
        if (Field(parent, path, key) is not { } value || TextOf(value, Join(path, key), 0, null) is not { } text)
        {
            return "";
        }

        if (!allowed.Contains(text))
        {
            Add(Join(path, key), "must be one of: " + string.Join(", ", allowed));
        }

        return text;
    }

    public bool Flag(JsonElement parent, string path, string key)
    {
        if (Field(parent, path, key) is not { } value)
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        Add(Join(path, key), "must be true or false");
        return false;
    }

    public int Whole(JsonElement parent, string path, string key, int minimum, int maximum)
    {
        if (Field(parent, path, key) is not { } value)
        {
            return 0;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            Add(Join(path, key), "must be a whole number");
            return 0;
        }

        if (number < minimum || number > maximum)
        {
            Add(Join(path, key), maximum == int.MaxValue ? $"must be at least {minimum}" : $"must be between {minimum} and {maximum}");
        }

        return number;
    }

    // The items of a list field with each item's path; empty when the field is absent or is not a list.
    public IReadOnlyList<(JsonElement Item, string Path)> Items(JsonElement parent, string path, string key, bool required = true)
    {
        if (Field(parent, path, key, required) is not { } value)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            Add(Join(path, key), "must be a list");
            return [];
        }

        return value.EnumerateArray().Select((item, index) => (item, $"{Join(path, key)}[{index}]")).ToList();
    }

    public IReadOnlyList<string> Texts(JsonElement parent, string path, string key, int minLength = 0) =>
        Items(parent, path, key).Select(entry => TextOf(entry.Item, entry.Path, minLength, null) ?? "").ToList();

    private string? TextOf(JsonElement value, string path, int minLength, int? maxLength)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            Add(path, "must be text");
            return null;
        }

        var text = value.GetString()!;
        if (text.Length < minLength)
        {
            Add(path, "must not be empty");
        }
        else if (maxLength is { } most && text.Length > most)
        {
            Add(path, $"must be at most {most} characters");
        }

        return text;
    }
}
