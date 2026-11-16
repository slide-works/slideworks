namespace SlideWorks.AcceptanceTests.Journeys;

// Completes once the page's /hubs/deck connection has finished a JoinDeck call, so a journey makes its agent's change
// only after the screen is listening. Without it the change can land between the screen's first read and its join, and
// the screen, which doesn't re-read on join, would miss it. Start it before the page navigates: it watches the
// SignalR frames the server sends back, and a completion message (type 3) answers the join.
public static class DeckHubJoin
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(15);

    public static Func<Task> Watch(IPage page)
    {
        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.WebSocket += (_, socket) =>
        {
            if (!socket.Url.Contains("/hubs/deck", StringComparison.Ordinal))
            {
                return;
            }
            socket.FrameReceived += (_, frame) =>
            {
                if (frame.Text?.Contains("\"type\":3", StringComparison.Ordinal) == true)
                {
                    joined.TrySetResult();
                }
            };
        };
        return async () =>
        {
            var first = await Task.WhenAny(joined.Task, Task.Delay(TIMEOUT));
            (first == joined.Task).ShouldBeTrue("The page never finished joining its deck on /hubs/deck.");
        };
    }
}
