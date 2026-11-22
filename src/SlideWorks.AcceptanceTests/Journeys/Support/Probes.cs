using System.Collections.Concurrent;

namespace SlideWorks.AcceptanceTests.Journeys.Support;

// Absence probes that cannot pass for nothing. A probe that finds nothing proves little unless the same probe is shown to
// find something once the thing is on the page, so PairedAsync runs the probe on the hidden surface, then lets the test
// show the thing, then runs it again, and returns both counts. The test asserts them inline (AG0012): Absent is 0 and
// Present is above 0. A probe that is broken (a wrong selector or script) fails the presence half.
public static class Probes
{
    // What the probe found before the thing was shown, and after.
    public sealed record Pair(IReadOnlyList<string> Absent, IReadOnlyList<string> Present);

    // Runs the script over the locator, then show, then the script again over the same locator, and returns what each run
    // found. show opens, reveals or advances whatever the test is probing for and waits for the page to render it.
    public static async Task<Pair> PairedAsync(ILocator surface, string script, Func<Task> show, object? arg = null)
    {
        var absent = await FindAsync(surface, script, arg);
        await show();
        var present = await FindAsync(surface, script, arg);
        return new Pair(absent, present);
    }

    // The strings the script returns for the locator's element (an array of what could carry a count, say).
    public static async Task<IReadOnlyList<string>> FindAsync(ILocator surface, string script, object? arg = null)
        => await surface.EvaluateAsync<string[]>(script, arg);

    // "The page has caught up": waits until the page's own connection has delivered more frames of the type than before
    // (count them with FrameCount first), so an absence probe that follows reads a page that has seen the change. Returns
    // the count it reached, which is at most the 5 seconds Eventually allows; the caller asserts it moved.
    public static Task<int> CaughtUpAsync(ConcurrentQueue<string> frames, string type, int before)
        => Eventually.ReadAsync(() => Task.FromResult(FrameCount(frames, type)), count => count > before);

    // How many frames of the type the connection has delivered so far (the type appears as "type":"<type>" in the frame).
    public static int FrameCount(ConcurrentQueue<string> frames, string type)
        => frames.Count(frame => frame.Contains($"\"type\":\"{type}\"", StringComparison.Ordinal));
}
