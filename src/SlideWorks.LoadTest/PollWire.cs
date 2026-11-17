using System.Text.Json;

namespace SlideWorks.LoadTest;

// The parts of the guest poll view (guest-poll.json) and the answer response the harness reads. Everything else, including
// fields added later, is ignored.
public static class PollWire
{
    public static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    public sealed record View(Activity? Activity, Mine? Mine, Tally? Tally);

    public sealed record Activity(string? Round, string? Phase, IReadOnlyList<string>? Options);

    public sealed record Mine(int Revision);

    public sealed record Tally(int Answered);

    public sealed record Refusal(string? Error);
}
