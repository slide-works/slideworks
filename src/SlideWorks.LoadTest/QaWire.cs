namespace SlideWorks.LoadTest;

// The parts of the guest questions view (guest-questions.json), the vote response (question-votes.json) and the ask response
// (question-ask.json) the qa scenario reads. Everything else, including fields added later, is ignored.
public static class QaWire
{
    public sealed record View(Room? Room, IReadOnlyList<Question>? Questions, Mine? Mine);

    public sealed record Room(string? Round, string? Questions, bool Reactions);

    public sealed record Question(int Number, string? State, int Votes, bool Yours);

    public sealed record Mine(int Revision);

    public sealed record Votes(IReadOnlyList<int>? Picks, int Revision);
}
