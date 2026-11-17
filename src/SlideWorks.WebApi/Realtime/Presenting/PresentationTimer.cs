namespace SlideWorks.WebApi.Realtime.Presenting;

// The talk timer (P1, D7). ElapsedMs is measured with the server's TimeProvider at the moment the message is sent; while
// the timer is running a client adds the performance.now() delta since it received the message.
public sealed record PresentationTimer(TimerState State, long ElapsedMs);
