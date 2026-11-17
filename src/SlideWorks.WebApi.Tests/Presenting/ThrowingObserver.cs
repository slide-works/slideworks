using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Presenting;

// A P10 observer that always throws, registered ahead of RecordingObserver, to prove a failing observer is logged and
// skipped: the call still succeeds, the group still gets the state, and the next observer is still told.
public sealed class ThrowingObserver : IPresentationObserver
{
    public Task OnChangedAsync(PresentationState state) => throw new InvalidOperationException("Observer failure (test).");
}
