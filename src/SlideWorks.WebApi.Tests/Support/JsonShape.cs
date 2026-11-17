using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Support;

// Checks a real response against a screen's JSON example in src/contracts/screens/. The property sets must be equal at
// every level, the value kinds must match, and every array element is checked against the example's first element.
// Null is allowed only at the listed nullable paths, and an opaque path's contents are not compared. Path syntax is in
// JsonShapeOptions.
public static class JsonShape
{
    public static void AssertMatches(JsonElement actual, string contractFile, params string[] nullablePaths) =>
        AssertMatches(actual, contractFile, new JsonShapeOptions { NullablePaths = nullablePaths });

    public static void AssertMatches(JsonElement actual, string contractFile, JsonShapeOptions options)
    {
        var path = Path.Combine(ScreensDirectory(), contractFile);
        File.Exists(path).ShouldBeTrue($"No contract example at {path}.");
        using var example = JsonDocument.Parse(File.ReadAllText(path));
        AssertMatches(actual, example.RootElement, contractFile, options);
    }

    public static void AssertMatches(JsonElement actual, JsonElement example, string exampleName, params string[] nullablePaths) =>
        AssertMatches(actual, example, exampleName, new JsonShapeOptions { NullablePaths = nullablePaths });

    public static void AssertMatches(JsonElement actual, JsonElement example, string exampleName, JsonShapeOptions options)
    {
        var problems = new List<string>();
        var rules = new Rules(
            new HashSet<string>(options.NullablePaths, StringComparer.Ordinal),
            new HashSet<string>(options.OpaquePaths, StringComparer.Ordinal));
        Compare(actual, example, "", rules, problems);
        problems.ShouldBeEmpty($"The JSON does not match {exampleName}");
    }

    // src/contracts/screens, found by walking up from the test binaries to the folder that holds the solution.
    public static string ScreensDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
            {
                return Path.Combine(directory.FullName, "contracts", "screens");
            }
        }
        throw new InvalidOperationException($"No SlideWorks.slnx above {AppContext.BaseDirectory}.");
    }

    private static void Compare(JsonElement actual, JsonElement example, string path, Rules rules, List<string> problems)
    {
        if (example.ValueKind == JsonValueKind.Null)
        {
            // An example that is null at a nullable path (an always-null field, or a sample of the empty case) matches null.
            if (actual.ValueKind == JsonValueKind.Null && rules.Nullable.Contains(path))
            {
                return;
            }
            problems.Add($"{Describe(path)}: the example is null; give it a non-null value so its kind is known");
            return;
        }
        if (actual.ValueKind == JsonValueKind.Null)
        {
            if (!rules.Nullable.Contains(path))
            {
                problems.Add($"{Describe(path)}: null, but null is not allowed here");
            }
            return;
        }
        if (KindOf(actual) != KindOf(example))
        {
            problems.Add($"{Describe(path)}: expected {KindOf(example)}, got {KindOf(actual)}");
            return;
        }
        if (rules.Opaque.Contains(path))
        {
            return;
        }
        if (example.ValueKind == JsonValueKind.Object)
        {
            CompareObjects(actual, example, path, rules, problems);
        }
        else if (example.ValueKind == JsonValueKind.Array)
        {
            CompareArrays(actual, example, path, rules, problems);
        }
    }

    private static void CompareObjects(JsonElement actual, JsonElement example, string path, Rules rules, List<string> problems)
    {
        var actualNames = actual.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var exampleNames = example.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(exampleNames.Where(name => !actualNames.Contains(name)).Select(name => $"{Describe(Child(path, name))}: missing"));
        problems.AddRange(actualNames.Where(name => !exampleNames.Contains(name)).Select(name => $"{Describe(Child(path, name))}: not in the example"));
        foreach (var property in example.EnumerateObject().Where(p => actualNames.Contains(p.Name)))
        {
            Compare(actual.GetProperty(property.Name), property.Value, Child(path, property.Name), rules, problems);
        }
    }

    private static void CompareArrays(JsonElement actual, JsonElement example, string path, Rules rules, List<string> problems)
    {
        if (actual.GetArrayLength() == 0)
        {
            return;
        }
        if (example.GetArrayLength() == 0)
        {
            problems.Add($"{Describe(path)}: the example array is empty; give it one element so its items can be checked");
            return;
        }
        var first = example[0];
        foreach (var element in actual.EnumerateArray())
        {
            Compare(element, first, path + "[]", rules, problems);
        }
    }

    private static string KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => element.ValueKind.ToString().ToLowerInvariant(),
    };

    private static string Child(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static string Describe(string path) => path.Length == 0 ? "(root)" : path;

    private sealed record Rules(HashSet<string> Nullable, HashSet<string> Opaque);
}
