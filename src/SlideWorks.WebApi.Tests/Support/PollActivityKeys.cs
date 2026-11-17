using System.Text.Json;

namespace SlideWorks.WebApi.Tests.Support;

// The keys a poll activity may carry to a guest (V5, poll-activity.json): the allow-list the privacy tests hold
// audienceState.activity to. Counts, Opens, Privacy and anything about other guests are not on it.
public static class PollActivityKeys
{
    public static IReadOnlyList<string> All { get; } =
        ["type", "round", "number", "phase", "question", "options", "pick", "max", "results", "roomSeesCounts", "correct"];

    // The keys of an activity as it reached the client: none when it is null.
    public static IReadOnlyList<string> Of(JsonElement activity)
        => activity.ValueKind == JsonValueKind.Object ? [.. activity.EnumerateObject().Select(p => p.Name)] : [];
}
