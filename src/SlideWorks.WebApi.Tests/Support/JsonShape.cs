using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Support;

// Checks a real response against a screen's JSON example in src/contracts/screens/. The property sets must be equal at
// every level, the value kinds must match, and every array element is checked against the example's first element.
// Null is allowed only at the listed paths, written like "decks[].subtitle" or "lastChange".
public static class JsonShape
{
    public static void AssertMatches(JsonElement actual, string contractFile, params string[] nullablePaths)
    {
        var path = Path.Combine(ContractsScreensDirectory(), contractFile);
        File.Exists(path).ShouldBeTrue($"No contract example at {path}.");
        using var example = JsonDocument.Parse(File.ReadAllText(path));
        AssertMatches(actual, example.RootElement, contractFile, nullablePaths);
    }

    public static void AssertMatches(JsonElement actual, JsonElement example, string exampleName, params string[] nullablePaths)
    {
        var problems = new List<string>();
        Compare(actual, example, "", new HashSet<string>(nullablePaths, StringComparer.Ordinal), problems);
        problems.ShouldBeEmpty($"The JSON does not match {exampleName}");
    }

    private static void Compare(JsonElement actual, JsonElement example, string path, HashSet<string> nullablePaths, List<string> problems)
    {
        if (example.ValueKind == JsonValueKind.Null)
        {
            problems.Add($"{Describe(path)}: the example is null; give it a non-null value so its kind is known");
            return;
        }
        if (actual.ValueKind == JsonValueKind.Null)
        {
            if (!nullablePaths.Contains(path))
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
        if (example.ValueKind == JsonValueKind.Object)
        {
            CompareObjects(actual, example, path, nullablePaths, problems);
        }
        else if (example.ValueKind == JsonValueKind.Array)
        {
            CompareArrays(actual, example, path, nullablePaths, problems);
        }
    }

    private static void CompareObjects(JsonElement actual, JsonElement example, string path, HashSet<string> nullablePaths, List<string> problems)
    {
        var actualNames = actual.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var exampleNames = example.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(exampleNames.Where(name => !actualNames.Contains(name)).Select(name => $"{Describe(Child(path, name))}: missing"));
        problems.AddRange(actualNames.Where(name => !exampleNames.Contains(name)).Select(name => $"{Describe(Child(path, name))}: not in the example"));
        foreach (var property in example.EnumerateObject().Where(p => actualNames.Contains(p.Name)))
        {
            Compare(actual.GetProperty(property.Name), property.Value, Child(path, property.Name), nullablePaths, problems);
        }
    }

    private static void CompareArrays(JsonElement actual, JsonElement example, string path, HashSet<string> nullablePaths, List<string> problems)
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
            Compare(element, first, path + "[]", nullablePaths, problems);
        }
    }

    private static string KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => element.ValueKind.ToString().ToLowerInvariant(),
    };

    private static string Child(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static string Describe(string path) => path.Length == 0 ? "(root)" : path;

    // src/contracts/screens, found by walking up from the test binaries to the folder that holds the solution.
    private static string ContractsScreensDirectory()
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
}
