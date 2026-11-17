using System.Diagnostics;

namespace SlideWorks.AcceptanceTests.Journeys;

// Retries a measurement the browser may still be settling (layout after fonts or an image load) until it reads as
// settled or the time runs out, and returns the last reading either way, so the caller's assertions report what was
// really there. For values a web-first Expect can't express, such as a probe's result.
public static class Eventually
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan INTERVAL = TimeSpan.FromMilliseconds(100);

    public static async Task<T> ReadAsync<T>(Func<Task<T>> read, Func<T, bool> settled)
    {
        var clock = Stopwatch.StartNew();
        var reading = await read();
        while (!settled(reading) && clock.Elapsed < TIMEOUT)
        {
            await Task.Delay(INTERVAL);
            reading = await read();
        }
        return reading;
    }
}
